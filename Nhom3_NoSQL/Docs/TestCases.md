# TEST CASE – KIỂM THỬ HỆ THỐNG SERVERPULSE

> Người thực hiện: Nguyễn Thanh Phong (2001230665) · Ngày chạy: 15/10/2026 (chạy lại toàn bộ sau khi sửa lỗi)
> Môi trường: Windows 11, MongoDB 8.3, .NET Framework 4.7.2, cửa sổ 1440×900.
> Dữ liệu: dữ liệu mẫu do `DemoDataSeeder` tạo (10 host, 7 ngày metric, 8 quy tắc ngưỡng, 6 sự cố) và 2 tài khoản Admin / Viewer.
> Bộ kiểm thử tự động viết bằng **C#**: project `Nhom3_NoSQL.Tests` (file `Program.cs`). Kết quả lần chạy gần nhất: `Nhom3_NoSQL.Tests/ket_qua_kiem_thu.txt`.

## Tổng hợp kết quả

| Nhóm kiểm thử | Số test case | PASS | FAIL | Cách thực hiện |
|---|---|---|---|---|
| Test API | 13 | 13 | 0 | Tự động (C#, gọi REST API) |
| Test đăng nhập / phân quyền (API) | 10 | 10 | 0 | Tự động (C#) |
| Test hiển thị Alert (API) | 6 | 6 | 0 | Tự động (C#) |
| Test logic giao diện (kiểm tra dữ liệu nhập, định dạng, CSV, font) | 9 | 9 | 0 | Tự động (C#) |
| Test giao diện WinForms (mở form thật, gọi API thật) | 9 | 9 | 0 | Tự động (C#) |
| **Tổng tự động** | **47** | **47** | **0** | |
| Test thủ công giao diện (chạy trên app thật) | 12 | 12 | 0 | Thao tác chuột/bàn phím thật trên app (mục 6) |
| **Tổng cộng** | **59** | **59** | **0** | |

**Cách chạy lại:** chạy app (F5) hoặc `Nhom3_NoSQL.exe --headless`, sau đó:
- 47 test tự động: chạy project `Nhom3_NoSQL.Tests` (hoặc `Nhom3_NoSQL.Tests.exe [http://localhost:5080]`).
- 6 test thủ công TC-MAN-07..12: `Nhom3_NoSQL.Tests.exe --thu-cong` (VS Code: cấu hình số 5). Chương trình mở form thật và **dùng chuột/bàn phím thật của Windows** để thao tác; trong lúc chạy (~1 phút) không dùng chuột. Ảnh bằng chứng + kết quả lưu ở `Docs/screenshots/kiem_thu_thu_cong/`.

Chương trình in PASS/FAIL từng test case, tổng kết, và trả mã thoát 0 khi tất cả PASS. Mọi exception phát sinh khi vẽ giao diện trong lúc chạy test cũng làm test đó FAIL.

---

## 1. Test API

| Mã TC | Mục tiêu | Các bước / Dữ liệu vào | Kết quả mong đợi | Kết quả |
|---|---|---|---|---|
| TC-API-01 | Lấy danh sách host | `GetHostsAsync()` (token Admin) | ≥ 9 host, mỗi host có `status` ∈ {ONLINE, WARNING, CRITICAL, OFFLINE} | PASS |
| TC-API-02 | Lọc host theo trạng thái | `GET /hosts?status=OFFLINE` | Chỉ trả host OFFLINE | PASS |
| TC-API-03 | Tìm host theo từ khóa | `GET /hosts?q=db-prod` | Đúng 2 host db-prod-01, db-prod-02 | PASS |
| TC-API-04 | Chi tiết host | `GET /hosts/{id}` | Đúng host | PASS |
| TC-API-05 | ID không hợp lệ | `GET /hosts/abc`; `GET /hosts/000000000000000000000000` | 400; 404 | PASS |
| TC-API-06 | Thêm / chống trùng / sửa host | `POST /hosts` (test-api-host, 10.99.0.1) → POST lại cùng tên → `PUT` sửa mô tả | Có Id → 409 → mô tả đã đổi | PASS |
| TC-API-07 | Kiểm tra dữ liệu host | Tên rỗng; IP `999.1.1` | 400; 400 | PASS |
| TC-API-08 | Agent gửi metric | `POST /metrics/collector` `{serverId, cpu:12.5, ram:40, disk:30}` (không token) | Host chuyển ONLINE, `latest.cpu = 12.5` | PASS |
| TC-API-09 | Collector kiểm tra dữ liệu | Host ID không tồn tại; `cpu: 150` | 404; 400 | PASS |
| TC-API-10 | Lịch sử metric | `GET /metrics/history/{id}?range=1h/6h/24h/7d` | Có điểm, ≤ 125 điểm, `cpuPeak ≥ cpuAvg` | PASS |
| TC-API-11 | Số liệu Dashboard (Aggregation) | `GET /overview` | `total = online + offline`, `series` ≥ 30 điểm (6 giờ / 10 phút), `avgCpu` ∈ [0,100] | PASS |
| TC-API-12 | Event Log | `GET /events?q=test-api-host`; `?type=AUTH` | Có sự kiện của host test; chỉ trả loại AUTH | PASS |
| TC-API-13 | Xóa host | `DELETE /hosts/{id}` → `GET` lại; xem sự cố của host | 404; không còn sự cố mở của host đã xóa | PASS |

## 2. Test đăng nhập / phân quyền (API)

| Mã TC | Mục tiêu | Các bước / Dữ liệu vào | Kết quả mong đợi | Kết quả |
|---|---|---|---|---|
| TC-AUTH-01 | Đăng nhập đúng | Tài khoản Admin | Có `token`, `role = Admin` | PASS |
| TC-AUTH-02 | Sai mật khẩu | Mật khẩu sai | 401 | PASS |
| TC-AUTH-03 | Email không tồn tại | khongco@x.vn | 401, cùng thông báo với TC-AUTH-02 (không lộ email nào tồn tại) | PASS |
| TC-AUTH-04 | Không có token | `GET /hosts` không header | 401 | PASS |
| TC-AUTH-05 | Token giả | `Authorization: Bearer fake-token` | 401 | PASS |
| TC-AUTH-06 | Viewer được xem | `GET /hosts` bằng Viewer | Có dữ liệu | PASS |
| TC-AUTH-07 | Viewer không được thêm host | `POST /hosts` bằng Viewer | 403 | PASS |
| TC-AUTH-08 | Viewer không được sửa ngưỡng | `PUT /thresholds/{id}` bằng Viewer | 403 | PASS |
| TC-AUTH-09 | Thông tin phiên | `GET /auth/me` | Đúng email đăng nhập | PASS |
| TC-AUTH-10 | Đăng xuất | `POST /auth/logout` rồi dùng lại token | 401 | PASS |

## 3. Test hiển thị Alert (API)

| Mã TC | Mục tiêu | Các bước / Dữ liệu vào | Kết quả mong đợi | Kết quả |
|---|---|---|---|---|
| TC-ALERT-01 | Danh sách + thống kê | `GET /alerts?hours=24` | Có `items` và `stats` (MTTA/MTTR) | PASS |
| TC-ALERT-02 | Lọc alert | `severity=Critical`; `status=RESOLVED` | Chỉ trả đúng loại | PASS |
| TC-ALERT-03 | Alert Engine sinh cảnh báo, không trùng | Tạo ngưỡng CPU 50/90, 0s cho host test → Agent gửi CPU 95, rồi 96 | Đúng 1 alert Critical, OPENING | PASS |
| TC-ALERT-04 | Viewer không được xử lý | Viewer gọi `/acknowledge` | 403 | PASS |
| TC-ALERT-05 | Vòng đời sự cố | Acknowledge → Acknowledge lại → Resolve → Resolve lại | ACKNOWLEDGED → 409 → RESOLVED → 409 | PASS |
| TC-ALERT-06 | Kiểm tra ngưỡng | critical < warning; % > 100 | 400; 400 | PASS |

## 4. Test logic giao diện

| Mã TC | Mục tiêu | Các bước / Dữ liệu vào | Kết quả mong đợi | Kết quả |
|---|---|---|---|---|
| TC-UI-LOGIN-01 | Kiểm tra email | `FormLogin.ValidateInput("abc", "x")` | “Email không hợp lệ” | PASS |
| TC-UI-LOGIN-02 | Bỏ trống mật khẩu | `ValidateInput(email, "")` | “Vui lòng nhập mật khẩu” | PASS |
| TC-UI-LOGIN-03 | Dữ liệu hợp lệ | `ValidateInput(email, "x")` | Không báo lỗi | PASS |
| TC-UI-HOST-01 | Form máy chủ | Hostname rỗng; IP `300.1`; IP `10.0.1` | “Hostname là bắt buộc”; “Địa chỉ IP không hợp lệ” ×2 | PASS |
| TC-UI-RULE-01 | Form ngưỡng | 90/80 %; 90/120 %; 0/10 % | “Critical phải ≥ Warning”; “% không được vượt 100”; “Ngưỡng phải lớn hơn 0” | PASS |
| TC-UI-FMT-01 | Định dạng hiển thị | 34,4 → %; 412.000 / 2.400.000 Kbps; 222 s; 5 giây trước | “34%”, “412 Mbps”, “2,4 Gbps”, “3m42s”, “5 giây trước” | PASS |
| TC-UI-FMT-02 | Màu trạng thái / ngưỡng | ONLINE, CRITICAL, ACKNOWLEDGED, OFFLINE; 95/85/50 với ngưỡng 80/90 | Xanh lá, đỏ, vàng, xám; đỏ/vàng/trắng | PASS |
| TC-UI-FONT-01 | Font dùng chung không bị hỏng | Vẽ nhãn trạng thái và thanh tài nguyên rồi dùng lại font | Font vẫn dùng được (không bị Dispose) – test hồi quy cho lỗi #9 | PASS |
| TC-UI-CSV-01 | Xuất CSV | Danh sách host + 1 host tên có dấu `"` | Dòng tiêu đề đúng, đủ số dòng, `"` được escape thành `""` | PASS |

## 5. Test giao diện WinForms (tự động – mở form thật, gọi API thật)

| Mã TC | Mục tiêu | Các bước | Kết quả mong đợi | Kết quả |
|---|---|---|---|---|
| TC-UI-LOGIN-04 | Đăng nhập sai trên form | Mở `FormLogin`, nhập mật khẩu sai, bấm Đăng nhập | Hiện “Email hoặc mật khẩu không đúng”, form không đóng | PASS |
| TC-UI-LOGIN-05 | Đăng nhập đúng trên form | Nhập tài khoản Admin | Form đóng với `DialogResult.OK`, phiên là Admin | PASS |
| TC-UI-DASH-01 | Dashboard | Mở `FormMain` → Tổng quan | Thẻ Tổng host / Offline khớp `GET /overview`; biểu đồ sức khỏe có 2 series, mỗi series ≥ 30 điểm | PASS |
| TC-UI-DASH-02 | Bảng máy chủ + lọc | Máy chủ → chọn “OFFLINE” ở bộ lọc | Bảng hiện 9 dòng/trang; sau khi lọc chỉ còn host OFFLINE | PASS |
| TC-UI-CHART-01 | Biểu đồ chi tiết host | Mở chi tiết web-prod-01 | Đúng 4 biểu đồ, mỗi biểu đồ > 10 điểm, trục Y của CPU/RAM/Disk tối đa 100, hiện tên host | PASS |
| TC-UI-ALERT-01 | Hiển thị Alert + quyền Viewer | Đăng nhập Viewer → Sự cố | Số “KẾT QUẢ” khớp API; nút Acknowledge/Resolve bị khóa; có ghi chú “Tài khoản Viewer chỉ được xem” | PASS |
| TC-UI-ALERT-02 | Xử lý sự cố trên giao diện | Tạo sự cố RAM cho host test → Sự cố → tìm “test-api-host” → bấm **Acknowledge** → bấm **Resolve** | DB lần lượt ACKNOWLEDGED, RESOLVED; giao diện hiện “Sự cố đã được xử lý” | PASS |
| TC-UI-ROLE-01 | Ẩn nút quản trị với Viewer | Viewer → Máy chủ, Ngưỡng cảnh báo | Không có nút “Thêm máy chủ”, “Tạo quy tắc” | PASS |
| TC-UI-NAV-01 | Điều hướng | Chuyển qua 5 trang; Nhật ký lọc ALERT | Không lỗi; Event Log chỉ có loại ALERT | PASS |

## 6. Test thủ công giao diện (thực hiện trên app thật)

TC-MAN-01..05 được thao tác trực tiếp trên app đang chạy, TC-MAN-06 do người dùng xác nhận và kiểm chứng thêm bằng click chuột hệ điều hành. TC-MAN-07..12 chạy bằng `Nhom3_NoSQL.Tests.exe --thu-cong`, dùng chuột/bàn phím thật của Windows trên form thật, kết quả kiểm tra qua API.

| Mã TC | Mục tiêu | Các bước | Kết quả mong đợi | Kết quả thực tế | Kết quả |
|---|---|---|---|---|---|
| TC-MAN-01 | Hiện/ẩn mật khẩu | Màn hình đăng nhập → bấm biểu tượng con mắt 4 lần liên tiếp | Mỗi lần bấm đổi hiện ↔ ẩn | Lần đầu: chỉ lần bấm thứ 2 mới có tác dụng (lỗi #10). Sau khi sửa: 4 lần bấm đổi đúng 4 lần | PASS |
| TC-MAN-02 | Phím Enter đăng nhập | Nhập tài khoản Admin → nhấn Enter | Đăng nhập như bấm nút | Vào màn hình Tổng quan | PASS |
| TC-MAN-03 | Tự làm mới | Mở Tổng quan, quan sát 12 giây | Đồng hồ chạy, số liệu thay đổi | Đồng hồ 08:30:06 → 08:30:19; CPU 43,4% → 43,6%, RAM 68,0% → 68,7% | PASS |
| TC-MAN-04 | Lọc bằng thẻ trạng thái | Máy chủ → bấm thẻ “Critical” | Thẻ viền xanh, bảng chỉ còn host CRITICAL | Còn 2 host db-prod-02, msi | PASS |
| TC-MAN-05 | Phím tắt tìm kiếm | Máy chủ → Ctrl+K → gõ “web” | Con trỏ vào ô tìm kiếm, còn 2 host web-prod | Còn web-prod-01, web-prod-02 | PASS |
| TC-MAN-06 | Xuất CSV | Máy chủ → Xuất CSV → lưu → mở bằng Excel | Hiện hộp thoại lưu, file mở được | Lần đầu nút bị thanh công cụ che (lỗi #12). Sau khi sửa: hiện hộp thoại “Save As”, người dùng xuất được file | PASS |
| TC-MAN-07 | Thêm / sửa / xóa host | Thêm host IP `300.1` → Lưu; sửa IP `10.0.0.99` → Lưu; bấm bút sửa mô tả; bấm thùng rác → Yes | Báo lỗi IP; lưu/sửa/xóa thành công; Event Log có dòng HOST | Báo “Địa chỉ IP không hợp lệ”; thêm, sửa, xóa đều đúng trong DB; Event Log có dòng Thêm/Cập nhật/Xóa | PASS |
| TC-MAN-08 | Đổi khoảng thời gian biểu đồ | Chi tiết web-prod-01 → bấm 1h, 24h, 7d | Biểu đồ vẽ lại; 7d đổi định dạng trục | 1h: 1 giờ dữ liệu; 24h: 23,8 giờ; 7d: 166,8 giờ, định dạng `dd/MM HH:mm` | PASS |
| TC-MAN-09 | Bật/tắt quy tắc | Ngưỡng → bấm công tắc quy tắc Disk của backup-hn-03 hai lần | Trạng thái đổi rồi trở lại, thẻ thống kê cập nhật | DB đổi trạng thái rồi trả về như cũ; thẻ “Quy tắc đang bật” cập nhật | PASS |
| TC-MAN-10 | Tạo quy tắc sai | Tạo quy tắc → Warning 90, Critical 80 → Lưu | Báo lỗi, không lưu | Báo “Ngưỡng Critical phải ≥ ngưỡng Warning”, số quy tắc giữ nguyên 8 | PASS |
| TC-MAN-11 | Thu nhỏ / phóng to | Thu nhỏ, khôi phục, phóng to cửa sổ | Không lỗi, bố cục co giãn | Biểu đồ 696px → 1020px khi phóng to, không có lỗi giao diện | PASS |
| TC-MAN-12 | Đăng xuất | Bấm avatar → Đăng xuất | Đóng màn hình chính, hủy phiên | Form đóng, phiên bị hủy (token xóa) | PASS |

---

## 7. Danh sách lỗi phát hiện và đã sửa trong quá trình kiểm thử

| # | Lỗi | Mức độ | Cách xử lý | Trạng thái |
|---|---|---|---|---|
| 1 | `MetricSnapshot` thiếu trường `_id`, đọc lịch sử metric từ MongoDB bị lỗi deserialize | Cao | Thêm `Id` + `[BsonIgnoreExtraElements]` cho các model | Đã sửa |
| 2 | Kết nối Redis thất bại làm app không khởi động được | Cao | Redis và Elasticsearch thành tùy chọn (`abortConnect=false`, bỏ qua khi không chạy) | Đã sửa |
| 3 | Alert Engine tạo cảnh báo lặp lại mỗi 5 giây khi chỉ số vẫn vượt ngưỡng | Cao | Không tạo mới khi host + metric còn sự cố chưa RESOLVED | Đã sửa |
| 4 | Biểu đồ (`Chart` của .NET) ném `ArgumentException: Height must be greater than 0px` khi bị co về 0 (thu nhỏ cửa sổ / lúc dựng trang) làm sập form | Cao | Lớp `SafeChart` bỏ qua resize/vẽ khi kích thước bằng 0 (phát hiện bởi TC-UI-CHART-01) | Đã sửa |
| 5 | Xóa host làm sự cố của host đó treo mãi ở OPENING | Trung bình | Khi xóa host thì đóng sự cố và xóa quy tắc riêng của host | Đã sửa |
| 6 | Lần làm mới chủ động bị bỏ qua nếu trùng lúc lần làm mới tự động đang chạy | Trung bình | Lần làm mới chủ động chờ lượt đang chạy xong rồi nạp lại | Đã sửa |
| 7 | App tắt một thời gian thì biểu đồ 6 giờ của host demo bị trống (bộ mô phỏng chỉ chạy khi app chạy) | Thấp | Khi khởi động, bù dữ liệu mô phỏng cho các khoảng trống (tối đa 7 ngày) | Đã sửa |
| 8 | ComboBox, thanh cuộn, tiêu đề bảng hiển thị nền trắng lệch tông | Thấp | `DarkCombo` tự vẽ, thanh cuộn/tiêu đề cửa sổ tối, tự vẽ hàng tiêu đề bảng | Đã sửa |
| 9 | Font dùng chung bị `Dispose` trong hàm vẽ nhãn trạng thái → các Label khác lỗi “Parameter is not valid” khi vẽ lại, làm gián đoạn thao tác (phát hiện khi click chuột thật) | Cao | Bỏ `using` với font trong cache; bộ test bắt mọi lỗi vẽ giao diện; thêm TC-UI-FONT-01 | Đã sửa |
| 10 | Biểu tượng con mắt bỏ lỡ lần bấm thứ 2 khi bấm nhanh (Windows tính là double-click) | Thấp | Xử lý ở `MouseDown` thay cho `Click` | Đã sửa |
| 11 | Cửa sổ 1440×900 lớn hơn màn hình laptop → phần dưới (và thông báo toast) bị khuất | Trung bình | Kích thước theo vùng làm việc của màn hình, tự phóng to khi màn hình nhỏ | Đã sửa |
| 12 | Nút “Xuất CSV” và “Lịch sử xử lý” bị thanh công cụ (Dock Fill) che, không bấm được | Cao | Đưa nút lên trên (`BringToFront`) | Đã sửa |
| 13 | Danh sách “Host cần chú ý” hiện thanh cuộn ngang thừa | Thấp | Các dòng luôn chừa chỗ cho thanh cuộn dọc | Đã sửa |
| 14 | Mở app lần thứ 2 khi đã có bản đang chạy → lỗi trùng cổng 5080, treo ở hộp báo lỗi bị khuất | Trung bình | Bản thứ 2 chỉ mở giao diện và kết nối vào server đang chạy; mọi lỗi chưa bắt được ghi `error.log` và báo lên | Đã sửa |
| 15 | Ô nhập không hỗ trợ Ctrl+A (TextBox .NET Framework) → không chọn-xóa nhanh được nội dung | Thấp | Thêm Ctrl+A cho mọi ô nhập | Đã sửa |
| 16 | Dòng mô tả của hộp thoại xuống 2 dòng bị ô nhập che | Thấp | Phần đầu hộp thoại tự cao theo nội dung | Đã sửa |
