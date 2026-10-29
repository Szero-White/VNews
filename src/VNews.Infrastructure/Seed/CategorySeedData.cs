namespace VNews.Infrastructure.Seed;

internal static class CategorySeedData
{
    public static readonly (string Name, string Slug, string Description, int Order)[] Categories =
    {
        ("Thời sự", "thoi-su", "Tin tức thời sự trong nước và quốc tế", 1),
        ("Kinh doanh", "kinh-doanh", "Tin tức kinh tế, tài chính, doanh nghiệp", 2),
        ("Thế giới", "the-gioi", "Tin tức quốc tế, bình luận toàn cầu", 3),
        ("Công nghệ", "cong-nghe", "Tin tức công nghệ, sản phẩm số", 4),
        ("Thể thao", "the-thao", "Tin tức thể thao trong nước và quốc tế", 5),
        ("Giải trí", "giai-tri", "Tin tức văn hóa, giải trí, showbiz", 6),
        ("Sức khỏe", "suc-khoe", "Tin tức y tế, sức khỏe, dinh dưỡng", 7),
        ("Giáo dục", "giao-duc", "Tin tức giáo dục, tuyển sinh, du học", 8),
        ("Du lịch", "du-lich", "Tin tức du lịch, điểm đến, ẩm thực", 9),
        ("Pháp luật", "phap-luat", "Tin tức pháp luật, an ninh, điều tra", 10),
    };
}
