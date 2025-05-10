using System.ComponentModel.DataAnnotations;

namespace CinemaTest.Models.Admin
{
    public class Room
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên phòng chiếu không được để trống")]
        [StringLength(100)]
        public string? Name { get; set; }

        [Required(ErrorMessage = "Số lượng ghế không được để trống")]
        [Range(1, 500, ErrorMessage = "Số ghế phải từ 1 đến 500")]
        public int SeatCount { get; set; }
    }
}
