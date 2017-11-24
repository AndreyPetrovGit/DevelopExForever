using System;
using System.ComponentModel.DataAnnotations;

namespace DevelopExParser.Models
{
    public class ScanerOptions
    {
        [Url(ErrorMessage = "Bad BaseUrl")]
        public String BaseUrl { get;  set; }

        [Range(1, Int32.MaxValue, ErrorMessage = "Bad ThreadCount")]
        public int ThreadCount { get; set; }

        [Required(AllowEmptyStrings = false, ErrorMessage = "Bad SearchedText")]
        [StringLength(Int32.MaxValue)]
        public String SearchedText { get; set; }

        [Range(1, Int32.MaxValue, ErrorMessage = "Bad ScanUrlCount")]
        public  Int32 ScanUrlCount { get; set; }
    }
}
