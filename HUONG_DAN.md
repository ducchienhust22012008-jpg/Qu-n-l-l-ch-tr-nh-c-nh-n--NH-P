# Hướng dẫn sử dụng Nhịp 2.9

## Bắt đầu

Mở `Nhip.exe` trong thư mục có quyền ghi. Bản sạch chưa có công việc, deadline, mục tiêu, phiên tập trung hoặc số phút thưởng. Dữ liệu mới được lưu trong thư mục `data` cạnh ứng dụng; không cần tài khoản.

## Công việc và lịch tuần

Bấm **+ Thêm việc**, nhập tên, nhóm, ngày, giờ, thời lượng và số phút thưởng. Có thể đặt 0 phút. Trong Lịch tuần, nhấp đúp ô trống để thêm theo ngày/giờ đã chọn; kéo block để chuyển lịch hoặc kéo mép để đổi thời lượng. Menu **⋯** hỗ trợ sửa, sao chép và đổi màu.

Tick khi hoàn thành để nhận thưởng một lần. Thay mức thưởng sau khi hoàn thành không cộng lại phần chênh lệch; bỏ tick thu hồi đúng số phút từng nhận. Nếu số phút đó đã được dùng, ví có thể âm; phần thưởng sau sẽ bù số dư âm trước.

## Deadline

Mở **Deadline → + Tạo deadline**, nhập tên và hạn. Không chọn giờ nghĩa là đến cuối ngày. Thêm việc nhỏ chưa xếp lịch hoặc gắn việc có sẵn. Mỗi việc thuộc tối đa một deadline.

Tiến độ tính bằng số việc hoàn thành/tổng số việc. Khi tất cả đã xong, bấm **Đã nộp / bàn giao** để xác nhận; deadline không nhận phần thưởng riêng. Bỏ hoàn thành một việc con sẽ tự mở lại deadline. Gỡ liên kết hoặc xóa deadline giữ lại công việc.

Trong **Lịch tuần → + Thêm việc → Chọn việc từ Deadline**, chọn deadline trước rồi chọn việc. Mặc định chỉ hiện việc chưa xếp lịch. Bộ lọc **Tất cả việc chưa xong** cho phép chọn việc đã có lịch; app hỏi trước khi chuyển lịch cũ. Thao tác này không tạo bản trùng hoặc cộng thưởng. Lịch sau hạn có cảnh báo để bạn quyết định lưu hoặc sửa lại.

## Bấm giờ và ví thời gian

Ở **Bấm giờ**, chọn một task chưa hoàn thành hoặc nhập tên phiên riêng. Chọn Học tập/Công việc, bắt đầu, tạm dừng khi nghỉ rồi lưu khi xong. Phiên chưa lưu được giữ khi đóng app bình thường. Lưu phiên chỉ ghi thời gian thực, không hoàn thành task hay cộng thưởng.

Ở **Ví thời gian**, bấm **Bắt đầu giải trí** để dùng phút thưởng; bấm Tạm dừng để ngừng trừ. Bắt đầu tập trung tự tạm dừng ví, bắt đầu giải trí tự tạm dừng tập trung.

## Mục tiêu tháng

Dùng **+ Thêm / Cập nhật** để tạo mục tiêu. Vòng tiến độ và hai nhóm Chưa hoàn thành/Đã hoàn thành cập nhật khi tick. Bấm tiêu đề nhóm để thu gọn. Tên dài xuống dòng; danh sách dài có vùng cuộn riêng. Mục tiêu không cộng phút vào ví.

## Thống kê

**Một ngày** có lịch chọn ngày, Hôm nay/Hôm qua, bốn chỉ số, dòng thời gian và ba tab Phiên tập trung/Công việc/Ví thời gian. Phiên qua nửa đêm được chia vào đúng ngày và thời gian nghỉ không được tính. Thống kê việc dùng ngày trong lịch cùng trạng thái hoàn thành hiện tại. Giải trí từ bản ghi cũ có thể được phân bổ gần đúng theo mốc lưu.

Các chế độ 7 ngày, 30 ngày, 12 tháng và Tùy chọn hỗ trợ xem dài hạn. Mục tiêu tháng được hiển thị theo tháng, độc lập với thời gian tập trung.

## Lưu và sao lưu

Thoát bình thường trước khi sao lưu hoặc chuyển ứng dụng. Sao chép cả thư mục `data/`; tệp `.bak` trong đó có thể được dùng để phục hồi khi dữ liệu chính hỏng. Không thay dữ liệu thật bằng mẫu trống nếu chưa giữ lại bản sao cần thiết.

Tệp `data.example.json` chỉ mô tả dữ liệu mặc định. Bản portable mới tự khởi tạo trống khi không có tệp dữ liệu. Các chế độ `--preview`, `--today-preview`, `--deadline-preview`, `--ui-test` tạo dữ liệu giả trong `verification/` và không thuộc chế độ sử dụng bình thường.
