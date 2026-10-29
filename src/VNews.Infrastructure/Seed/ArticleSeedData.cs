using VNews.Domain.Entities;

namespace VNews.Infrastructure.Seed;

internal static class ArticleSeedData
{
    private static readonly string[] Authors =
    {
        "Minh Anh", "Hoàng Long", "Thu Trang", "Quốc Bảo", "Lan Phương",
        "Đức Huy", "Ngọc Hà", "Văn Sơn", "Thanh Tâm", "Gia Khánh"
    };

    private static readonly (string CategorySlug, string[] Headlines)[] Templates =
    {
        ("thoi-su", new[]
        {
            "Hà Nội khởi công tuyến đường vành đai mới giảm ùn tắc nội đô",
            "TP.HCM thí điểm mô hình chính quyền số tại 5 quận trung tâm",
            "Quốc hội thảo luận dự án luật sửa đổi về đất đai",
            "Miền Trung chủ động ứng phó áp thấp nhiệt đới trên Biển Đông",
            "Chương trình hỗ trợ nhà ở cho công nhân khu công nghiệp được mở rộng"
        }),
        ("kinh-doanh", new[]
        {
            "Doanh nghiệp xuất khẩu gỗ ghi nhận tăng trưởng hai con số",
            "Ngân hàng Nhà nước giữ nguyên lãi suất điều hành tháng này",
            "Thị trường bán lẻ trong nước phục hồi mạnh dịp cuối năm",
            "Startup công nghệ nông nghiệp nhận vốn đầu tư vòng Series A",
            "Giá vàng trong nước biến động theo diễn biến thị trường quốc tế"
        }),
        ("the-gioi", new[]
        {
            "Hội nghị khí hậu quốc tế đạt thỏa thuận cắt giảm khí thải",
            "Kinh tế khu vực châu Á - Thái Bình Dương dự báo tăng trưởng ổn định",
            "Các nước ASEAN tăng cường hợp tác an ninh hàng hải",
            "Liên minh châu Âu công bố gói hỗ trợ năng lượng sạch",
            "Căng thẳng thương mại giữa các nền kinh tế lớn có dấu hiệu hạ nhiệt"
        }),
        ("cong-nghe", new[]
        {
            "Trợ lý ảo thế hệ mới hỗ trợ xử lý công việc văn phòng",
            "Doanh nghiệp Việt đẩy mạnh chuyển đổi số trong sản xuất",
            "Smartphone tầm trung ra mắt với cải tiến về pin và camera",
            "Xu hướng làm việc từ xa thúc đẩy nhu cầu phần mềm cộng tác",
            "An ninh mạng trở thành ưu tiên hàng đầu của các tổ chức năm nay"
        }),
        ("the-thao", new[]
        {
            "Đội tuyển quốc gia giành chiến thắng thuyết phục ở vòng loại",
            "Giải marathon thành phố thu hút hàng nghìn vận động viên",
            "Câu lạc bộ bóng đá trẻ đầu tư bài bản cho mùa giải mới",
            "Vận động viên điền kinh lập thành tích cá nhân tại giải châu Á",
            "Bóng chuyền nữ Việt Nam khởi đầu thuận lợi tại giải vô địch"
        }),
        ("giai-tri", new[]
        {
            "Phim điện ảnh Việt đạt doanh thu ấn tượng sau hai tuần công chiếu",
            "Liên hoan âm nhạc mùa thu quy tụ nhiều nghệ sĩ trẻ",
            "Chương trình truyền hình thực tế về ẩm thực gây sốt mạng xã hội",
            "Nghệ sĩ trẻ ra mắt album đầu tay với phong cách mới lạ",
            "Triển lãm nghệ thuật đương đại mở cửa miễn phí cho công chúng"
        }),
        ("suc-khoe", new[]
        {
            "Bác sĩ khuyến cáo chế độ dinh dưỡng hợp lý cho người cao tuổi",
            "Bệnh viện tuyến tỉnh triển khai kỹ thuật phẫu thuật ít xâm lấn",
            "Chiến dịch tiêm chủng mở rộng được triển khai tại nhiều địa phương",
            "Thói quen vận động giúp cải thiện sức khỏe tim mạch",
            "Cảnh báo gia tăng bệnh theo mùa khi thời tiết chuyển lạnh"
        }),
        ("giao-duc", new[]
        {
            "Kỳ thi tuyển sinh lớp 10 dự kiến điều chỉnh cấu trúc đề thi",
            "Nhiều trường đại học công bố phương án tuyển sinh năm tới",
            "Chương trình học bổng du học mở rộng đối tượng áp dụng",
            "Giáo viên vùng cao sáng tạo phương pháp dạy học trực quan",
            "Ứng dụng học trực tuyến hỗ trợ học sinh ôn tập hiệu quả"
        }),
        ("du-lich", new[]
        {
            "Điểm đến miền núi phía Bắc hút khách dịp đầu mùa se lạnh",
            "Du lịch biển miền Trung khởi động mùa cao điểm cuối năm",
            "Ẩm thực đường phố trở thành điểm nhấn thu hút du khách quốc tế",
            "Tour trải nghiệm làng nghề truyền thống ngày càng được ưa chuộng",
            "Ngành du lịch đặt mục tiêu đón lượng khách quốc tế tăng trưởng"
        }),
        ("phap-luat", new[]
        {
            "Cơ quan chức năng tăng cường kiểm tra an toàn thực phẩm dịp lễ",
            "Tòa án xét xử vụ án kinh tế liên quan nhiều bị cáo",
            "Công an triệt phá đường dây lừa đảo qua mạng quy mô lớn",
            "Quy định mới về xử phạt vi phạm giao thông bắt đầu có hiệu lực",
            "Chương trình tuyên truyền pháp luật được triển khai tại cơ sở"
        }),
    };

    public static List<Article> Build(List<Category> categories)
    {
        var articles = new List<Article>();
        var baseDate = DateTime.UtcNow.Date.AddDays(-45);
        var counter = 0;

        foreach (var (categorySlug, headlines) in Templates)
        {
            var category = categories.First(c => c.Slug == categorySlug);

            foreach (var headline in headlines)
            {
                counter++;
                var publishedAt = baseDate.AddDays(counter % 40).AddHours(counter % 12);
                var author = Authors[counter % Authors.Length];
                var slug = Slugify(headline) + "-" + counter;

                articles.Add(new Article
                {
                    Title = headline,
                    Slug = slug,
                    Summary = BuildSummary(headline),
                    Body = BuildBody(headline, category.Name),
                    CoverImageUrl = $"/images/demo/{categorySlug}.svg",
                    AuthorName = author,
                    Status = ArticleStatus.Published,
                    PublishedAt = publishedAt,
                    CreatedAt = publishedAt,
                    ViewCount = (counter * 37) % 5000 + 120,
                    CategoryId = category.Id,
                    Category = category
                });
            }
        }

        return articles;
    }

    private static string BuildSummary(string headline) =>
        $"{headline}. Bài viết minh họa nội dung demo của VNews, phục vụ mục đích trình bày danh mục và trang chi tiết bài báo.";

    private static string BuildBody(string headline, string categoryName)
    {
        return $"""
        <p>{headline} là nội dung minh họa thuộc chuyên mục <strong>{categoryName}</strong> trên VNews, được xây dựng cho mục đích trình diễn portfolio.</p>
        <p>Trong giai đoạn thử nghiệm, tòa soạn VNews tập trung xây dựng quy trình biên tập, xuất bản và hiển thị nội dung theo thời gian thực từ cơ sở dữ liệu SQL Server thông qua Entity Framework Core.</p>
        <p>Các số liệu, tên nhân vật và chi tiết trong bài là dữ liệu hư cấu, không phản ánh sự kiện thực tế, chỉ nhằm minh họa định dạng trình bày bài báo gồm tiêu đề, tóm tắt, nội dung chính và chuyên mục liên quan.</p>
        <p>Phiên bản chính thức của VNews sẽ được cập nhật nội dung biên tập thật khi hệ thống quản trị (CMS) hoàn thiện ở giai đoạn tiếp theo.</p>
        """;
    }

    private static string Slugify(string input)
    {
        var normalized = input.ToLowerInvariant();
        var map = new Dictionary<string, string>
        {
            {"à","a"},{"á","a"},{"ả","a"},{"ã","a"},{"ạ","a"},
            {"ă","a"},{"ằ","a"},{"ắ","a"},{"ẳ","a"},{"ẵ","a"},{"ặ","a"},
            {"â","a"},{"ầ","a"},{"ấ","a"},{"ẩ","a"},{"ẫ","a"},{"ậ","a"},
            {"è","e"},{"é","e"},{"ẻ","e"},{"ẽ","e"},{"ẹ","e"},
            {"ê","e"},{"ề","e"},{"ế","e"},{"ể","e"},{"ễ","e"},{"ệ","e"},
            {"ì","i"},{"í","i"},{"ỉ","i"},{"ĩ","i"},{"ị","i"},
            {"ò","o"},{"ó","o"},{"ỏ","o"},{"õ","o"},{"ọ","o"},
            {"ô","o"},{"ồ","o"},{"ố","o"},{"ổ","o"},{"ỗ","o"},{"ộ","o"},
            {"ơ","o"},{"ờ","o"},{"ớ","o"},{"ở","o"},{"ỡ","o"},{"ợ","o"},
            {"ù","u"},{"ú","u"},{"ủ","u"},{"ũ","u"},{"ụ","u"},
            {"ư","u"},{"ừ","u"},{"ứ","u"},{"ử","u"},{"ữ","u"},{"ự","u"},
            {"ỳ","y"},{"ý","y"},{"ỷ","y"},{"ỹ","y"},{"ỵ","y"},
            {"đ","d"}
        };

        foreach (var pair in map)
        {
            normalized = normalized.Replace(pair.Key, pair.Value);
        }

        var chars = normalized.Select(c => char.IsLetterOrDigit(c) || c == ' ' ? c : ' ').ToArray();
        var cleaned = new string(chars);
        var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join('-', parts);
    }
}
