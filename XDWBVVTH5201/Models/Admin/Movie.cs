using System.ComponentModel.DataAnnotations;

namespace CinemaTest.Models.Admin
{
    public class Movie
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên phim")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn thể loại")]
        public string Genre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mô tả")]
        public string Description { get; set; } = string.Empty;

        [Range(1, 500, ErrorMessage = "Thời lượng phim phải từ 1 đến 500 phút")]
        public int Duration { get; set; }

        [Url(ErrorMessage = "Vui lòng nhập đúng định dạng URL trailer")]
        public string TrailerUrl { get; set; } = string.Empty;

        [ScaffoldColumn(false)]
        public string PosterUrl { get; set; } = string.Empty;
    }
}
