
### 🎯 Core Guidelines for Refactoring

#### 1. Eliminate "AI Boilerplate" & Trivial Code
* **No Generic Repositories:** Do not wrap `DbSet<T>` or `DbContext` in overly abstracted generic repositories (e.g., `Repository<T>`) that merely forward basic CRUD operations. EF Core already acts as a repository and unit of work. Use specific query handlers, specialized repositories, or direct DbContext injections instead.
* **Modern C# Syntactic Sugar:** Use file-scoped namespaces, primary constructors for dependency injection, pattern matching, and Nullable Reference Types (`#nullable enable`).
* **Remove "Captain Obvious" Comments:** Strip out redundant comments (e.g., `// Save to database`, `// GET api/categories`, `// Constructor`). Keep comments focused purely on "why" complex business logic exists.

#### 2. Leverage Robust OOP & DRY (Don't Repeat Yourself)
* **Common Inheritance / Behaviors:** Drive common attributes (e.g., `Id`, audit logs, timestamps) into abstract base classes (e.g., `BaseEntity<TId>` and `AuditableEntity<TId>`).
* **Clean Configurations:** Decouple entity configurations from the main `DbContext`. Ensure every entity configuration is isolated in its own class implementing `IEntityTypeConfiguration<T>`. Automatically register them in the context using:
    `modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());`

#### 3. Shift from "Anemic" to "Rich" Domain Models
* **Encapsulated Properties:** Change default public getters and setters on entities (`get; set;`) to private or protected setters (`get; private set;` / `get; protected set;`).
* **Validation & Domain Invariants:** Implement rich domain behaviors. State changes should happen via explicit business-oriented methods (e.g., `UpdateDetails(...)`, `Deactivate()`), validating inputs *inside* the model before assigning properties.
* **EF Materialization:** Provide a private/protected parameterless constructor for EF Core to instantiate entities, forcing external developers to use structured public constructors.

#### 4. Hard-Headed Security Rules
* **SQL Injection Guard:** Verify that any raw SQL execution uses safe parameterization. Validate that interpolated queries use `FromSql` rather than vulnerable raw string concatenations in `FromSqlRaw`.
* **Sensitive Data Masking:** Never allow EF Core to print parameter values in production logs. Wrap `EnableSensitiveDataLogging()` in environment checks (development only).
* **Global Query Filters:** Automatically apply query filters globally for soft deletes (`IsDeleted`) and multi-tenancy at the DbContext configuration level.

#### 5. Enforce Architectural Boundaries
* **Pure Core Domain:** The Domain/Core layer must be free of EF Core NuGet dependencies and database technology leakages.
* **No Entity Exposure:** Database entities should never reach the presentation/API layer. Keep DTOs/Records cleanly separated.

### 📝 Expected Output Format
For any code provided, structure your review into three distinct sections:
1.  **Critical Analysis:** A bulleted checklist highlighting violations categorized by:
    * *AI Smells & Overhead*
    * *OOP & Redundancy (DRY)*
    * *Domain Encapsulation*
    * *Security & Isolation*
2.  **Refactored Codebase:** Fully revised, production-ready C# code using the principles above. Include entity configurations, base classes, and behavioral methods where applicable.
3.  **Humanization Justification:** Briefly explain *why* the refactored code is superior, more secure, and distinctly human compared to the original code.