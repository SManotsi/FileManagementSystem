using FileManagementSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Net.Mail;
using System.Net;

namespace FileManagementSystem.Controllers
{
    public class FileController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FileController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Action to list all files
        public async Task<IActionResult> Index()
        {
            var files = await _context.Files.ToListAsync();
            return View(files);
        }

        // Action to show the upload form
        public IActionResult Upload()
        {
            return View();
        }

        // Action to handle file upload
        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file, string uploadedBy = "DefaultUser", bool isShared = false, string? sharedWithEmail = null)
        {
            if (file != null && file.Length > 0)
            {
                try
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var filePath = Path.Combine(uploadsFolder, file.FileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var fileModel = new FileModel
                    {
                        FileName = file.FileName,
                        FilePath = file.FileName,
                        FileSize = file.Length,
                        UploadDate = DateTime.Now,
                        UploadedBy = uploadedBy,
                        IsShared = isShared,
                        SharedWithEmail = isShared ? sharedWithEmail : null
                    };

                    _context.Files.Add(fileModel);
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error uploading file: {ex.Message}");
                    return View();
                }
            }

            ModelState.AddModelError("", "Please select a file to upload.");
            return View();
        }

        // Action to view a file
        public async Task<IActionResult> View(int id)
        {
            var file = await _context.Files.FindAsync(id);
            if (file == null)
                return NotFound("File not found in the database.");

            if (string.IsNullOrEmpty(file.FilePath))
                return NotFound("The file path is null or empty in the database.");

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", file.FilePath);

            if (!System.IO.File.Exists(filePath))
                return NotFound("The file does not exist on the server.");

            var contentType = GetContentType(filePath);
            if (contentType == null)
                return BadRequest("Unsupported file type.");

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;
            return File(memory, contentType);
        }

        // Action to handle file download
        public async Task<IActionResult> Download(int id)
        {
            var file = await _context.Files.FindAsync(id);
            if (file == null) return NotFound();

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", file.FileName);

            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(fileBytes, "application/octet-stream", file.FileName);
        }

        // Action to show the Edit form
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var file = await _context.Files.FindAsync(id);
            if (file == null)
                return NotFound("File not found in the database.");

            return View(file);
        }

        // Action to handle Edit form submission
        [HttpPost]
        public async Task<IActionResult> Edit(int id, FileModel updatedFile)
        {
            if (id != updatedFile.Id)
            {
                return BadRequest("File ID mismatch.");
            }

            if (!ModelState.IsValid)
            {
                return View(updatedFile);
            }

            try
            {
                var existingFile = await _context.Files.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
                if (existingFile == null)
                    return NotFound("File not found during update.");

                updatedFile.FilePath = existingFile.FilePath; // Preserve original file path
                updatedFile.FileSize = existingFile.FileSize; // Preserve original file size

                _context.Update(updatedFile);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Files.Any(f => f.Id == id))
                {
                    return NotFound("File not found during update.");
                }
                throw;
            }
        }

        // Action to show Delete confirmation
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var file = await _context.Files.FindAsync(id);
            if (file == null)
                return NotFound("File not found in the database.");

            return View(file);
        }

        // Action to handle file deletion
        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var file = await _context.Files.FindAsync(id);
            if (file == null)
                return NotFound("File not found in the database.");

            try
            {
                _context.Files.Remove(file);
                await _context.SaveChangesAsync();

                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", file.FilePath);
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error deleting file: {ex.Message}");
            }
        }

        // Action to share file via email
        public async Task<IActionResult> Share(int id)
        {
            var file = await _context.Files.FindAsync(id);
            if (file == null) return NotFound();

            return View(file);
        }

        // Action to handle sharing a file via email
        [HttpPost]
        public async Task<IActionResult> Share(int id, string email)
        {
            var file = await _context.Files.FindAsync(id);
            if (file == null) return NotFound();

            try
            {
                var fromEmail = "smanotsi@gmail.com"; // Replace with your email address
                var fromPassword = "#Servant1"; // Replace with your email password (or app password)
                var smtpHost = "smtp.gmail.com"; // SMTP server for Gmail
                var smtpPort = 587;

                using (var mail = new MailMessage())
                {
                    mail.From = new MailAddress(fromEmail);
                    mail.To.Add(email);
                    mail.Subject = "Shared File: " + file.FileName;
                    mail.Body = $"A file has been shared with you: {file.FileName}";

                    // Attach the file
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", file.FilePath); // Correct file path
                    var attachment = new Attachment(filePath);
                    mail.Attachments.Add(attachment);

                    using (var smtp = new SmtpClient(smtpHost, smtpPort))
                    {
                        smtp.Credentials = new NetworkCredential(fromEmail, fromPassword);
                        smtp.EnableSsl = true;
                        await smtp.SendMailAsync(mail);
                    }
                }

                // Update the file with shared status and email address
                file.IsShared = true;
                file.SharedWithEmail = email;
                _context.Update(file);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error sending email: {ex.Message}");
                return View(file);
            }

            return RedirectToAction(nameof(Index));
        }


        // Helper method to determine content type
        private string GetContentType(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            var extension = Path.GetExtension(path)?.ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".txt" => "text/plain",
                ".doc" or ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" or ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" or ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                _ => null,
            };
        }
    }
}
