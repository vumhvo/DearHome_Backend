# DearHome – Backend API

REST API chính của **DearHome**, nền tảng thương mại điện tử nội thất có hỗ trợ xem sản phẩm 3D/AR và trợ lý AI. Dịch vụ này quản lý người dùng, sản phẩm, kho, đơn hàng, thanh toán, vận chuyển, khuyến mãi, đánh giá và thống kê. Mỗi khi dữ liệu thay đổi, nó phát sự kiện qua **NATS** để AI Backend đồng bộ.

> Hệ thống DearHome gồm 3 repo:
>
> | Repo | Vai trò |
> |------|---------|
> | [DearHome-Frontend](https://github.com/vumhvo/DearHome-Frontend) | Web app React cho khách hàng và quản trị viên |
> | **DearHome_Backend** (repo này) | REST API chính (ASP.NET Core, SQL Server) |
> | [DearHome-AI_Backend](https://github.com/vumhvo/DearHome-AI_Backend) | Chatbot AI, sinh mô hình 3D, gợi ý bố trí phòng (FastAPI) |

## Kiến trúc

```
                    ┌──────────────────────┐
  Frontend ───────▶ │   DearHome_Backend   │ ──▶ SQL Server (INTERIOR_SHOPPING)
  (REST + JWT)      │  ASP.NET Core 9 API  │ ──▶ Firebase Storage (ảnh, mô hình 3D)
                    └──────────┬───────────┘ ──▶ PayOS (thanh toán), GoShip (vận chuyển), SMTP (email)
                               │ NATS: product.*, variant.*, order.*, promotion.*
                               ▼
                    ┌──────────────────────┐
                    │ DearHome-AI_Backend  │ ──▶ ChromaDB (vector search cho chatbot)
                    └──────────────────────┘
```

Mã nguồn chia theo các lớp **Controller → Service → Repository → EF Core DbContext**, dùng AutoMapper để chuyển giữa Model và DTO.

## Công nghệ

- **.NET 9** / ASP.NET Core Web API, Swagger (Swashbuckle)
- **Entity Framework Core 9** + SQL Server, ASP.NET Core Identity (khoá `Guid`)
- **JWT Bearer** và đăng nhập Google (`Google.Apis.Auth`)
- **AutoMapper**
- **Firebase Admin SDK** / Firebase Storage để lưu ảnh và file `.glb`
- **PayOS** (cổng thanh toán), **GoShip** (tính phí và tạo vận đơn)
- **NATS** (`NATS.Client`) để phát sự kiện thay đổi dữ liệu
- Cache: bộ nhớ trong khi Development, **Redis** khi Production
- **Azure Application Insights** để log và theo dõi

## Tính năng chính

| Nhóm | Mô tả |
|------|-------|
| Người dùng | Đăng ký (mã xác thực qua email), đăng nhập, đăng nhập Google, đổi mật khẩu, hồ sơ, địa chỉ; vai trò `Admin` / `User` / `Guest`; hạng khách hàng Bronze → Diamond |
| Danh mục & thuộc tính | Danh mục cha/con theo slug, thuộc tính và giá trị thuộc tính gắn với danh mục, vị trí đặt (placement) |
| Sản phẩm & biến thể | CRUD sản phẩm, biến thể kèm thuộc tính, ảnh và mô hình 3D; phân trang |
| Kho | Phiếu nhập hàng (Good Received Note) để cập nhật tồn kho |
| Đơn hàng | Tạo đơn, cập nhật trạng thái (`Placed → WaitForPayment → Processing → Shipping → Delivered → Completed`, hoặc `Cancelled`), lịch sử đơn theo người dùng |
| Thanh toán | Tạo link thanh toán PayOS, webhook và xác minh thanh toán |
| Vận chuyển | Tính phí ship, danh sách tỉnh/huyện/xã qua GoShip |
| Khuyến mãi | Mã giảm giá theo thời gian và hạng khách hàng |
| Đánh giá | Đánh giá sản phẩm theo đơn hàng |
| Thống kê | Doanh thu, số đơn, sản phẩm bán chạy, tỷ lệ trạng thái đơn, tồn kho theo danh mục, hiệu quả khuyến mãi |

## Cấu trúc thư mục

```
DearHome_Backend/
├── Config/          # Cấu hình NATS
├── Constants/       # Enum: trạng thái đơn, vai trò, hạng khách hàng, chủ đề NATS…
├── Controllers/     # API endpoint (route: api/[controller])
├── DTOs/            # Đối tượng request/response
├── Data/            # DearHomeContext và cấu hình Fluent API cho từng entity
├── Helpers/         # AutoMapper profile, đăng ký DI (ServiceExtensions)
├── Middlewares/     # Xử lý lỗi tập trung
├── Models/          # Entity EF Core
├── Repositories/    # Interfaces + Implementations truy cập dữ liệu
├── Services/        # Interfaces + Implementations nghiệp vụ
└── Program.cs       # Khởi tạo ứng dụng
```

## Bắt đầu

### Yêu cầu

- [.NET SDK 9.0](https://dotnet.microsoft.com/download)
- SQL Server (bản local, Docker hoặc Azure SQL)
- Tài khoản dịch vụ Firebase (file JSON service account)
- (Tuỳ chọn) NATS server, tài khoản PayOS, GoShip, SMTP

Ví dụ chạy SQL Server và NATS bằng Docker:

```bash
docker run -d --name sqlserver -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='<mật-khẩu-mạnh>' -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
```

```bash
docker run -d --name nats -p 4222:4222 nats:latest
```

### Cấu hình

`appsettings*.json` đã bị bỏ qua trong `.gitignore`, nên bạn cần tự tạo `DearHome_Backend/appsettings.Development.json`:

```jsonc
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=INTERIOR_SHOPPING;User Id=sa;Password=<mật-khẩu>;TrustServerCertificate=True"
  },
  "Jwt": {
    "Key": "<chuỗi-bí-mật-dài-ít-nhất-32-ký-tự>",
    "Issuer": "http://localhost:5000/",
    "Audience": "http://localhost:5000/"
  },
  "Firebase": {
    "ApiKey": "<firebase-web-api-key>",
    "StorageBucket": "<project-id>.appspot.com"
  },
  "PayOS": {
    "BaseUrl": "https://api-merchant.payos.vn",
    "PAYOS_CLIENT_ID": "<client-id>",
    "PAYOS_API_KEY": "<api-key>",
    "PAYOS_CHECKSUM_KEY": "<checksum-key>"
  },
  "Goship": {
    "BaseUrl": "<goship-api-url>",
    "ApiSecret": {
      "username": "<username>",
      "password": "<password>",
      "client_id": "<client-id>",
      "client_secret": "<client-secret>"
    }
  },
  "SmtpSettings": {
    "Host": "smtp.gmail.com",
    "Port": "587",
    "Username": "<email>",
    "Password": "<app-password>",
    "FromEmail": "<email>",
    "FromName": "DearHome"
  },
  "Redis": {
    "ConnectionString": "<chỉ cần cho Production>",
    "InstanceName": "DearHome_"
  },
  "ApplicationInsights": {
    "ConnectionString": "<tuỳ chọn>"
  }
}
```

Lưu ý:

- **Firebase service account**: đặt file JSON vào thư mục `DearHome_Backend/Credentials/`. Tên file đang được khai báo cứng trong [`Program.cs`](DearHome_Backend/Program.cs) (biến `firebaseCredentialPath`), hãy sửa cho khớp với file của bạn.
- **JWT**: `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience` phải **trùng** với `JWT_SECRET_KEY`, `JWT_ISSUER`, `JWT_AUDIENCE` của AI Backend, vì AI Backend xác thực cùng token này.
- **NATS**: thông tin kết nối lấy từ giá trị mặc định trong [`Config/NatsConfig.cs`](DearHome_Backend/Config/NatsConfig.cs) (`nats://0.0.0.0:4222`).

### Tạo cơ sở dữ liệu

Repo chưa có sẵn thư mục migration, hãy tạo migration lần đầu rồi áp dụng:

```bash
dotnet tool install --global dotnet-ef
cd DearHome_Backend
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### Chạy

```bash
cd DearHome_Backend
dotnet restore
dotnet run --launch-profile http
```

API chạy tại `http://localhost:5226` (profile `https`: `https://localhost:7042`). Ở môi trường Development, **Swagger UI** mở ngay tại trang gốc `http://localhost:5226/`.

## Danh sách API

Mọi endpoint có tiền tố `/api`. Xem chi tiết tham số trong Swagger.

| Controller | Endpoint tiêu biểu |
|------------|--------------------|
| `User` | `POST login`, `POST login-google`, `POST register`, `POST send-verification-code`, `POST logout`, `GET me`, `GET all-customers`, `PUT change-password` |
| `Category` | `GET`, `GET parent-categories`, `GET get-by-slug/{slug}`, `GET with-parent-and-attributes`, `POST`, `PUT`, `DELETE {id}` |
| `Attribute` | `GET`, `GET with-attribute-values`, `GET with-attribute-values-by-category/{categoryId}`, `POST`, `PUT`, `DELETE {id}` |
| `Placement` | CRUD vị trí đặt sản phẩm |
| `Product` | `GET` (phân trang), `GET {id}`, `GET top-sales`, `GET get-by-category/{id}`, `GET with-variants`, `POST`, `PUT`, `DELETE {id}` |
| `Variant` | `GET`, `GET {id}`, `GET get-by-product/{productId}`, `POST`, `PUT`, `DELETE {id}` |
| `GoodReceivedNote` | `GET`, `GET {id}`, `POST`, `PUT` |
| `Order` | `GET`, `GET {id}`, `POST create?returnUrl=…`, `PUT update-status/{id}`, `GET user/{userId}`, `GET user-and-status/{userId}` |
| `Payment` | `GET get-payment-link-infomation`, `POST verify-webhook`, `POST verify-payment` |
| `Shipping` | `POST calculate-shipping-cost`, `GET get-cities`, `GET get-districts/{cityId}`, `GET get-wards/{districtId}` |
| `Promotion` | `GET`, `GET {id}`, `GET usable/{userId}`, `POST`, `PUT {id}` |
| `Review` | `GET`, `GET order/{orderId}`, `GET product/{productId}`, `POST`, `PUT {id}`, `DELETE {id}` |
| `FirebaseStorage` | `POST upload-image`, `PUT update-image`, `DELETE delete-image` |
| `Stats` | `GET sales`, `GET order-counts`, `GET overall`, `GET top-sales-products`, `GET order-status-percentage`, `GET categories-stock`, `GET promotion-statistics` |

File [`DearHome_Backend.http`](DearHome_Backend/DearHome_Backend.http) có thể dùng để gửi request thử ngay trong VS Code / Rider.

## Sự kiện NATS

Khi dữ liệu thay đổi, API phát các sự kiện sau (định nghĩa trong [`Constants/NatsConstants.cs`](DearHome_Backend/Constants/NatsConstants.cs)). AI Backend lắng nghe để cập nhật kho vector cho chatbot.

| Đối tượng | Chủ đề |
|-----------|--------|
| Sản phẩm | `product.created`, `product.updated`, `product.deleted` |
| Biến thể | `variant.created`, `variant.updated`, `variant.deleted` |
| Đơn hàng | `order.created`, `order.updated`, `order.deleted` |
| Khuyến mãi | `promotion.created`, `promotion.updated`, `promotion.deleted` |

## Triển khai

File [`app.yaml`](app.yaml) cấu hình triển khai lên **Google App Engine (flexible, runtime `aspnetcore`)** với `ASPNETCORE_ENVIRONMENT=Production`. Ở Production, ứng dụng dùng Redis làm distributed cache, vì vậy cần khai báo `Redis:ConnectionString`. Các khoá bí mật nên được cấp qua biến môi trường (ví dụ `Jwt__Key`, `ConnectionStrings__DefaultConnection`) thay vì đưa vào file.
