# Catalog seed

`catalog.json` chứa 100 Product, 381 ProductItem, 10 Brand, 13 Category (3 cấp), 4 Collection và 5 Bundle.

- Mỗi Product có 1, 2 hoặc 10 item. Product không có variation có một item mặc định với `selections: {}`; Domain hiện không có cờ `IsDefault`.
- Có Product với hai variation Màu sắc × Kích cỡ, đủ 10 tổ hợp riêng biệt.
- Bundle nằm trong `products[].items[].components`, tham chiếu SKU của item thường; quantity phải dương. Thứ tự Product trong JSON không ảnh hưởng tham chiếu bundle.
- Hai Collection thủ công tham chiếu `products[].key`; hai Collection tự động dùng Brand/Category với `All` hoặc `Any`. Category được đối chiếu trực tiếp, không tự gồm các category con.
- `media: ""` tại Brand, Product và ProductItem bỏ qua media. Điền URL hợp lệ để tạo logo hoặc media khi seed lần đầu. Category và Collection hiện không có media trong Domain.
- Tên thương hiệu là dữ liệu giả lập. Toàn bộ Product/Collection được publish, ProductItem được activate để dễ thử các API đọc.

## Chạy

Từ thư mục solution, sau khi database đã có schema Catalog tương ứng với các migration hiện tại:

```powershell
dotnet run --project src/Bootstrappers/UltraSol.Bootstrappers -- --Catalog:Seed=true
```

Lệnh dùng cấu hình kết nối hiện có của ứng dụng, seed xong thì thoát. Chạy API thông thường không tự seed. JSON được sao chép vào `seeds/catalog.json` trong thư mục build/publish; phần thực thi nằm tại Infrastructure/Persistence/Seeding.

Bộ seed đọc và dựng toàn bộ aggregate qua Domain trước khi ghi. Một transaction của CatalogUnitOfWork và advisory lock hiện có bảo vệ toàn bộ lần ghi. Không tự tạo database hoặc áp dụng migration.

Nếu tất cả SKU đã tồn tại, lệnh bỏ qua và không cập nhật dữ liệu. Nếu chỉ một phần SKU trùng, lệnh báo lỗi và không ghi gì. Đây là bộ nạp dữ liệu mẫu một lần, không phải công cụ đồng bộ: sửa JSON sau lần seed thành công sẽ không cập nhật các bản ghi đã tồn tại. Dùng database phát triển sạch nếu muốn nạp lại phiên bản JSON đã sửa.

## Kiểm tra không cần database

```powershell
dotnet test tests/UltraSol.Modules.Catalog.Domain.Tests --filter FullyQualifiedName~CatalogSeedTests
```