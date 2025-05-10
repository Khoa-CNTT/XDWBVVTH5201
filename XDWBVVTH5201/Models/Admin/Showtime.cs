using CinemaTest.Models.Admin;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaTest.Models.Admin
{
    public class Showtime
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Phim không được để trống.")]
        public int MovieId { get; set; }

        [Required(ErrorMessage = "Phòng chiếu không được để trống.")]
        public int RoomId { get; set; }

        [Required(ErrorMessage = "Thời gian bắt đầu không được để trống.")]
        public DateTime StartTime { get; set; }

        [Required(ErrorMessage = "Thời gian kết thúc không được để trống.")]
        public DateTime EndTime { get; set; }

        [ForeignKey("MovieId")]
        public virtual Movie? Movie { get; set; }

        [ForeignKey("RoomId")]
        public virtual Room? Room { get; set; }
    }
}
