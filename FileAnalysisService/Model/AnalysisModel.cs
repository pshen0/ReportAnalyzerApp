using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FileAnalysisService.Model;

[Table("AnalysisModels")]
public class AnalysisModel
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }
    
    [Required]
    public Guid FileId { get; set; }
    
    [Required]
    public int Paragraphs { get; set; }
    
    [Required]
    public int Words { get; set; }
    
    [Required]
    public int Characters { get; set; }
    
    [Column(TypeName = "text")]
    public string WordCloudUrl { get; set; } = string.Empty;
    
    [Required]
    public DateTime AnalysedAt { get; set; }
}