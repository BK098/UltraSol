# Catalog query APIs

Các API này phục vụ quản trị: mặc định không ẩn Draft, Inactive, Unpublished hoặc Archived.
Không có thay đổi authentication trong đợt này; không coi các endpoint này là API storefront đã được bảo vệ.

## Quy ước

- Thành công: HTTP 200, `ApiResult<T>`. Danh sách dùng `ApiResult<PaginatedResult<T>>`.
- Query danh sách nhận `pageIndex=1&pageSize=10&search=...`, pageSize từ 1 đến 200.
- Search tên không phân biệt hoa/thường; ProductItem/Bundle search SKU; options search Value; media search AltText (hoặc URL nếu không có AltText).
- Sort ổn định Name rồi Id; item/bundle theo SKU rồi Id; media theo SortOrder rồi Id; sản phẩm trong Manual Collection theo SortOrder rồi Id.
- ID rỗng, enum hoặc paging không hợp lệ trả 400. Entity/owner không tồn tại hoặc child không thuộc đúng owner trả 404.
- Không ép chỉ trạng thái hiển thị công khai. Collection products là danh sách quản trị: Manual theo membership; Automatic theo rules All/Any và category trực tiếp, không bao gồm category con.
- HTTP response của mỗi query là kiểu riêng, kể cả các DTO lồng nhau. Các kiểu trong `Application.Reads` chỉ là dữ liệu đọc trung gian, không được trả trực tiếp.
- GetAll không có Description, toàn bộ media, options hay components. Product list vẫn có category names, brand và ảnh primary; item list có SKU, Product, selections có tên và ảnh primary.
- Detail chứa metadata và dữ liệu con của aggregate. Product Detail không nhúng ProductItems; Brand/Category/Collection Detail không nhúng tất cả products hay cây category. Dùng endpoint liên quan bên dưới.
- Lists cấp aggregate và product theo quan hệ được count/filter/page trong SQL. Danh sách con (media/options/categories/components của một owner) được đọc từ owner, rồi search/page trong bộ nhớ; không quét toàn Catalog.

## Endpoints

| Endpoint | Kết quả |
| --- | --- |
| `GET /api/products` | Phân trang |
| `GET /api/products/{productId}` | Chi tiết |
| `GET /api/brands` | Phân trang |
| `GET /api/brands/{brandId}` | Chi tiết |
| `GET /api/brands/{brandId}/products` | Phân trang |
| `GET /api/categories` | Phân trang |
| `GET /api/categories/{categoryId}` | Chi tiết |
| `GET /api/categories/{categoryId}/products` | Phân trang |
| `GET /api/collections` | Phân trang |
| `GET /api/collections/{collectionId}` | Chi tiết |
| `GET /api/collections/{collectionId}/products` | Phân trang |
| `GET /api/categories/{categoryId}/children` | Phân trang |
| `GET /api/products/{productId}/items` | Phân trang |
| `GET /api/product-items` | Phân trang |
| `GET /api/products/{productId}/items/{productItemId}` | Chi tiết |
| `GET /api/bundles` | Phân trang |
| `GET /api/products/{productId}/items/{productItemId}/bundle` | Chi tiết |
| `GET /api/products/{productId}/variations` | Phân trang |
| `GET /api/products/{productId}/variations/{variationId}` | Chi tiết |
| `GET /api/products/{productId}/media` | Phân trang |
| `GET /api/products/{productId}/categories` | Phân trang |
| `GET /api/products/{productId}/items/{productItemId}/media` | Phân trang |
| `GET /api/products/{productId}/items/{productItemId}/bundle/components` | Phân trang |
| `GET /api/products/{productId}/variations/{variationId}/options` | Phân trang |
| `GET /api/products/{productId}/media/{mediaId}` | Chi tiết |
| `GET /api/products/{productId}/items/{productItemId}/media/{mediaId}` | Chi tiết |
| `GET /api/products/{productId}/variations/{variationId}/options/{optionId}` | Chi tiết |

## Bộ lọc bổ sung

- Products và product theo Brand/Category/Collection: `status=Draft|Published|Unpublished|Archived`.
- Products GetAll còn nhận `brandId`, `categoryId`; khi cùng truyền thì giao hai điều kiện.
- Brands: `isActive`, `isArchived`.
- Categories: `parentId`, `rootsOnly`, `isArchived`; không dùng parentId cùng rootsOnly=true. Category children chỉ trả con trực tiếp.
- Collections: `status`, `type=Manual|Automatic`.
- ProductItems: `status=Draft|Active|Inactive|Archived`, `isBundle`. Global list tại `/api/product-items`; danh sách theo owner nằm dưới Product.
- Bundles: `status`, `productId`; chỉ các ProductItem có bundle definition.
- Không có filter trạng thái: trả tất cả, kể cả tham chiếu tới Brand/Category đã Archived.

## JSON mới và ví dụ

Đây là thay đổi JSON được chấp thuận, client cũ cần cập nhật:
- Product list: thay BrandId/BrandName bằng `brand: { id, name }` hoặc null.
- Selections: `{ variationId, variationName, optionId, optionValue }`.
- ProductItem và Bundle có `product: { id, name }`. Thành phần bundle có Id, SKU, Product, Status, Quantity và PrimaryMediaUrl.
- Category có `parent: { id, name }` hoặc null.
- Collection detail có Type/Status/MatchMode dạng tên enum, Brands/Categories là mảng Id/Name.
- Các status/type enum trong response là chuỗi, không phải số.
- Không trả audit fields, concurrency token hoặc OptionSignature.

```http
GET /api/products?pageIndex=1&pageSize=10&search=shirt&status=Draft
GET /api/brands?pageIndex=1&pageSize=10&isArchived=false
GET /api/categories?rootsOnly=true&pageIndex=1&pageSize=10
GET /api/product-items?pageIndex=1&pageSize=10&search=SKU&isBundle=false
GET /api/bundles?pageIndex=1&pageSize=10
```

Các route có ID yêu cầu Guid thật; xem Swagger để biết response schema riêng của từng endpoint.

## Kiểm thử

Tests sử dụng fake read store để kiểm tra MediatR/validation/JSON, và PostgreSQL provider với interceptor chặn kết nối để kiểm tra dịch SQL, paging và filter.
Không chạy migration, không ghi database, chưa thay thế kiểm thử tích hợp với PostgreSQL thực tế.