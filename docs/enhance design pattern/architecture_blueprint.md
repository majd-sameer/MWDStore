# C# .NET + Angular 22 Solution Architecture Specification

This document provides a highly structured directory blueprint for a decoupled, clean architecture application consisting of an Angular 22 Client-Side Rendered (CSR) Admin Panel, an Angular 22 Server-Side Rendered (SSR) Storefront, and a .NET Domain-Driven Design (DDD) Core Backend.

## 📦 Directory Blueprint

```text
YourSolution/
├── 1.Domain/                  # Pure Business Logic (Zero External Dependencies)
│   ├── Entities/              # Domain Models with Private Setters (e.g., Product.cs, ContentBlock.cs)
│   ├── ValueObjects/          # Immutable Primitive Wrapper Objects (e.g., Money.cs, Address.cs)
│   ├── Exceptions/            # Domain-Specific Exceptions (e.g., DomainException.cs)
│   └── Interfaces/            # Domain-Level Repository Contracts (Optional if using raw EF Core DbSets)
│
├── 2.Application/             # Use Cases & Orchestration Logic (Depends ONLY on Domain)
│   ├── Common/                # Application-wide Behaviors (e.g., Validation, Logging Pipeline Behaviors)
│   ├── Dtos/                  # Data Transfer Objects for API Contracts
│   ├── Admin/                 # Admin Module Use Cases (Command-Heavy)
│   │   ├── Commands/          # State-changing operations (e.g., CreateProduct, UpdateContentBlock)
│   │   └── Mappers/           # Admin-specific data transformers
│   └── Storefront/            # Storefront Module Use Cases (Query-Heavy)
│       └── Queries/           # Read-only operations optimized for performance (e.g., GetLandingPageData)
│
├── 3.Infrastructure/          # Framework-Specific & External Infrastructure Concrete Implementations
│   ├── Persistence/           # Data Storage Implementations
│   │   ├── ApplicationDbContext.cs
│   │   └── Configurations/    # Entity Framework Core Fluent API Mappings (IEntityTypeConfiguration<T>)
│   └── Services/              # External integrations (e.g., RedisCache.cs, S3FileStorageService.cs)
│
├── 4.Presentation/            # Entry Points, Clients, and API Interfaces
│   ├── Web.API/               # ASP.NET Core Host Project
│   │   ├── Controllers/       # API Routing Topography
│   │   │   ├── Admin/         # Route endpoints heavily guarded by [Authorize(Roles = "Admin")]
│   │   │   └── Public/        # Optimized, publicly accessible cacheable storefront routes
│   │   └── Program.cs         # Dependency Injection Composition Root & Middleware Configuration
│   │
│   ├── client-admin/          # Angular 22 Client-Side Rendered (CSR) Administration Dashboard
│   └── client-storefront/     # Angular 22 Server-Side Rendered (SSR) E-Commerce & Landing Interface
```

## 🛠 Architectural Rules

1. **Inward Dependencies:** All dependencies must point inward. `Domain` relies on nothing. `Application` relies only on `Domain`. `Infrastructure` and `Presentation` rely on `Application` and `Domain`.
2. **CQRS Segregation:** Do not mix heavy reporting queries with transactional writes. Optimize the `Storefront/Queries` path using projections (`.Select()`) and `.AsNoTracking()`.
3. **Pure Domain Elements:** Database configurations, tables attributes, and ORM infrastructure markers belong exclusively inside `Infrastructure/Persistence/Configurations/`. Keep elements in `1.Domain/Entities/` clean.