namespace FileManagementSystem.Models
{
    public class FileModel
    {
        public int Id { get; set; }
        public string? FileName { get; set; }
        public required string FilePath { get; set; }
        public long FileSize { get; set; }
        public DateTime? UploadDate { get; set; }
        public string? UploadedBy { get; set; }
        public bool IsShared { get; set; } // Optional column for shared status
        public string? SharedWithEmail { get; set; } // Optional column for shared email
       
    }
}
