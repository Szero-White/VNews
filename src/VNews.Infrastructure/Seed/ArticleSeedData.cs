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

    private sealed record CongNgheArticle(string Title, string Summary, string Body, string ImageFileName, string AuthorName);

    private static readonly CongNgheArticle[] CongNgheArticles =
    {
        new(
            "Trợ lý ảo thế hệ mới hỗ trợ xử lý công việc văn phòng",
            "Các nền tảng trợ lý ảo tích hợp AI tạo sinh bắt đầu đảm nhận việc soạn email, tóm tắt cuộc họp và lên lịch, giúp nhân sự văn phòng rút ngắn đáng kể thời gian xử lý tác vụ lặp lại.",
            """
            <p>Nhiều doanh nghiệp tại Việt Nam đang thử nghiệm trợ lý ảo tích hợp trí tuệ nhân tạo tạo sinh vào quy trình làm việc hằng ngày, từ soạn thảo văn bản, tóm tắt nội dung cuộc họp đến lập lịch công tác.</p>
            <p>Khác với các chatbot hỏi đáp đơn thuần trước đây, thế hệ trợ lý ảo mới có khả năng hiểu ngữ cảnh công việc, truy xuất tài liệu nội bộ và đề xuất bước xử lý tiếp theo mà không cần nhân sự mô tả lại toàn bộ yêu cầu từ đầu.</p>
            <p>Một số đơn vị công nghệ trong nước cho biết việc ứng dụng trợ lý ảo giúp giảm khoảng 20-30% thời gian xử lý các tác vụ hành chính lặp lại, song vẫn cần con người kiểm duyệt trước khi gửi đi để tránh sai sót.</p>
            <p>Giới chuyên gia khuyến nghị doanh nghiệp xây dựng quy chế rõ ràng về dữ liệu được phép đưa vào trợ lý ảo, đặc biệt với thông tin khách hàng và tài liệu nội bộ nhạy cảm.</p>
            """,
            "tro-ly-ao-van-phong.jpg",
            "Ngọc Hà"),
        new(
            "Doanh nghiệp Việt đẩy mạnh chuyển đổi số trong sản xuất",
            "Từ dây chuyền lắp ráp đến kho vận, nhiều nhà máy trong nước đang lắp đặt cảm biến và phần mềm giám sát thời gian thực nhằm giảm hao hụt nguyên liệu và rút ngắn thời gian dừng máy.",
            """
            <p>Tại các khu công nghiệp phía Bắc và phía Nam, không ít doanh nghiệp sản xuất đã bắt đầu lắp đặt cảm biến IoT trên dây chuyền để theo dõi hiệu suất máy móc theo thời gian thực, thay vì chỉ ghi nhận thủ công theo ca.</p>
            <p>Dữ liệu thu thập được đưa vào phần mềm phân tích giúp bộ phận vận hành phát hiện sớm dấu hiệu hỏng hóc, từ đó lên lịch bảo trì chủ động thay vì chờ máy dừng hẳn mới sửa chữa.</p>
            <p>Theo đại diện một số nhà máy, khoản đầu tư ban đầu cho hệ thống giám sát số hóa thường thu hồi vốn trong 12-18 tháng nhờ giảm thời gian dừng máy ngoài kế hoạch và hao hụt nguyên vật liệu.</p>
            <p>Thách thức lớn nhất hiện nay vẫn là đào tạo lại lao động vận hành để làm quen với bảng điều khiển số, thay vì thao tác hoàn toàn thủ công như trước.</p>
            """,
            "chuyen-doi-so-san-xuat.jpg",
            "Văn Sơn"),
        new(
            "Smartphone tầm trung ra mắt với cải tiến về pin và camera",
            "Phân khúc smartphone tầm trung đang là điểm cạnh tranh chính giữa các hãng, khi người dùng ngày càng ưu tiên thời lượng pin và chất lượng camera hơn là chạy đua cấu hình.",
            """
            <p>Nhiều mẫu smartphone tầm trung mới trình làng gần đây tập trung cải thiện hai yếu tố người dùng phổ thông quan tâm nhất: thời lượng pin và chất lượng chụp ảnh trong điều kiện thiếu sáng.</p>
            <p>Thay vì chạy đua thông số chip xử lý như phân khúc cao cấp, các hãng lựa chọn dung lượng pin lớn hơn kết hợp sạc nhanh, đồng thời trang bị cảm biến camera chính kích thước lớn hơn để xử lý ảnh đêm tốt hơn.</p>
            <p>Giá bán của nhóm sản phẩm này tại thị trường trong nước dao động quanh mức phổ biến với đa số người dùng, giúp nhóm máy tầm trung tiếp tục chiếm tỷ trọng lớn trong tổng doanh số điện thoại bán ra.</p>
            <p>Giới phân tích nhận định xu hướng này phản ánh đúng nhu cầu thực tế: phần lớn người dùng phổ thông ưu tiên độ bền pin và trải nghiệm chụp ảnh hằng ngày hơn là hiệu năng chơi game nặng.</p>
            """,
            "smartphone-tam-trung.jpg",
            "Thanh Tâm"),
        new(
            "Xu hướng làm việc từ xa thúc đẩy nhu cầu phần mềm cộng tác",
            "Mô hình làm việc kết hợp giữa văn phòng và từ xa đã trở thành tiêu chuẩn mới tại nhiều công ty công nghệ, kéo theo nhu cầu tăng mạnh với các nền tảng họp trực tuyến và quản lý công việc theo nhóm.",
            """
            <p>Sau giai đoạn thử nghiệm làm việc từ xa toàn phần, nhiều công ty công nghệ tại Việt Nam chuyển sang mô hình kết hợp: nhân sự đến văn phòng vài ngày trong tuần, thời gian còn lại làm việc tại nhà.</p>
            <p>Mô hình này kéo theo nhu cầu rõ rệt với các công cụ cộng tác trực tuyến như phần mềm họp video, bảng công việc dùng chung và lưu trữ tài liệu đồng bộ theo thời gian thực.</p>
            <p>Không ít doanh nghiệp vừa và nhỏ cho biết việc chuẩn hóa quy trình báo cáo tiến độ qua phần mềm giúp quản lý dễ theo dõi hiệu suất nhóm hơn, dù vẫn cần duy trì các buổi gặp trực tiếp định kỳ để gắn kết đội ngũ.</p>
            <p>Bộ phận nhân sự tại một số công ty cũng lưu ý cần có quy định rõ về giờ phản hồi khi làm việc từ xa, tránh tình trạng nhân sự bị yêu cầu trực tuyến ngoài giờ làm việc chính thức.</p>
            """,
            "lam-viec-tu-xa.jpg",
            "Gia Khánh"),
        new(
            "An ninh mạng trở thành ưu tiên hàng đầu của các tổ chức năm nay",
            "Số vụ tấn công mã hóa dữ liệu tống tiền nhắm vào doanh nghiệp vừa và nhỏ gia tăng, buộc nhiều tổ chức phải dành ngân sách riêng cho an ninh mạng thay vì xem đây là chi phí phụ.",
            """
            <p>Theo ghi nhận từ một số đơn vị cung cấp dịch vụ bảo mật trong nước, số vụ tấn công mã hóa dữ liệu đòi tiền chuộc nhắm vào doanh nghiệp vừa và nhỏ có xu hướng tăng so với cùng kỳ năm trước, do nhóm này thường đầu tư hạn chế cho hạ tầng bảo mật.</p>
            <p>Hình thức tấn công phổ biến vẫn là lừa đảo qua email giả mạo đối tác hoặc cấp trên, nhằm chiếm quyền truy cập ban đầu trước khi phát tán mã độc trong toàn hệ thống nội bộ.</p>
            <p>Nhiều tổ chức bắt đầu đưa an ninh mạng thành khoản đầu tư cố định hằng năm, bao gồm đào tạo nhận thức bảo mật cho nhân viên, sao lưu dữ liệu định kỳ tách biệt khỏi mạng chính và diễn tập xử lý sự cố.</p>
            <p>Chuyên gia bảo mật khuyến cáo doanh nghiệp nên áp dụng xác thực đa lớp cho toàn bộ tài khoản quan trọng, thay vì chỉ dựa vào mật khẩu đơn lẻ như trước đây.</p>
            """,
            "an-ninh-mang.jpg",
            "Minh Anh"),
        new(
            "Việt Nam ra mắt nền tảng AI hỗ trợ báo chí",
            "Nền tảng mới cho phép tòa soạn tự động hóa khâu biên tập sơ bộ, kiểm tra chính tả và gợi ý tiêu đề, trong khi quyết định xuất bản cuối cùng vẫn thuộc về biên tập viên con người.",
            """
            <p>Một nền tảng trí tuệ nhân tạo hỗ trợ báo chí vừa được giới thiệu tại Việt Nam, hướng tới hỗ trợ các tòa soạn trong khâu biên tập sơ bộ như kiểm tra chính tả, gợi ý tiêu đề và tóm tắt nhanh nội dung dài.</p>
            <p>Nền tảng không thay thế vai trò biên tập viên mà đóng vai trò trợ lý: toàn bộ bài viết do AI gợi ý chỉnh sửa vẫn phải qua bước duyệt của con người trước khi xuất bản, nhằm đảm bảo tính chính xác và trách nhiệm biên tập.</p>
            <p>Đại diện nhóm phát triển cho biết công cụ được huấn luyện ưu tiên giữ nguyên văn phong gốc của phóng viên, thay vì viết lại toàn bộ câu chữ, để tránh làm mất đi góc nhìn cá nhân trong bài viết.</p>
            <p>Trong giai đoạn đầu, nền tảng được một số tòa soạn thử nghiệm ở khâu hỗ trợ biên tập nội bộ, chưa áp dụng cho các nội dung điều tra hoặc bài viết đòi hỏi kiểm chứng nguồn tin phức tạp.</p>
            """,
            "ai-ho-tro-bao-chi.jpg",
            "Đức Huy"),
        new(
            "Tòa soạn số hóa quy trình xuất bản để rút ngắn thời gian đưa tin",
            "Việc chuyển từ quy trình duyệt bài thủ công sang hệ thống quản trị nội dung trực tuyến giúp tòa soạn rút ngắn thời gian từ lúc phóng viên nộp bài đến khi bài được đăng tải công khai.",
            """
            <p>Nhiều tòa soạn đang chuyển đổi quy trình xuất bản truyền thống sang hệ thống quản trị nội dung (CMS) trực tuyến, cho phép phóng viên nộp bài, biên tập viên duyệt và xuất bản ngay trên cùng một nền tảng thay vì trao đổi qua nhiều kênh rời rạc.</p>
            <p>Quy trình số hóa giúp mỗi bài viết đi qua các bước rõ ràng: soạn thảo, gửi duyệt, biên tập viên phê duyệt hoặc trả lại chỉnh sửa, trước khi xuất bản công khai trên trang chủ và chuyên mục liên quan.</p>
            <p>Việc ghi nhận trạng thái từng bài viết theo thời gian thực cũng giúp quản lý tòa soạn theo dõi khối lượng công việc của từng phóng viên và biên tập viên rõ ràng hơn so với cách quản lý bằng email hoặc bảng tính thủ công.</p>
            <p>Đại diện một số đơn vị báo chí số cho biết việc rút ngắn quy trình nội bộ không làm giảm chất lượng kiểm duyệt, mà ngược lại giúp biên tập viên có thêm thời gian tập trung vào nội dung thay vì xử lý thao tác hành chính.</p>
            """,
            "newsroom-xuat-ban-so.jpg",
            "Hoàng Long"),
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
                    CoverImageUrl = $"/images/demo/articles/{counter}.jpg",
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

        var congNghe = categories.First(c => c.Slug == "cong-nghe");
        foreach (var item in CongNgheArticles)
        {
            counter++;
            var publishedAt = baseDate.AddDays(counter % 40).AddHours(counter % 12);
            var slug = Slugify(item.Title) + "-" + counter;

            articles.Add(new Article
            {
                Title = item.Title,
                Slug = slug,
                Summary = item.Summary,
                Body = item.Body,
                CoverImageUrl = $"/images/demo/cong-nghe/{item.ImageFileName}",
                AuthorName = item.AuthorName,
                Status = ArticleStatus.Published,
                PublishedAt = publishedAt,
                CreatedAt = publishedAt,
                ViewCount = (counter * 37) % 5000 + 120,
                CategoryId = congNghe.Id,
                Category = congNghe
            });
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
