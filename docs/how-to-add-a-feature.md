# How to add a feature

The **projects** feature is the reference implementation. Copy its structure; each step below links the file to
copy. Rules behind each step are in [AGENTS.md](../AGENTS.md) and the linked ADRs.

As an example, the steps add a hypothetical `Customer` feature.

## 1. Domain entity

`src/Boilerplate.Domain/Customers/Customer.cs`, like [`Project.cs`](../src/Boilerplate.Domain/Projects/Project.cs):

- Derive from `Entity` (GUID v7 id). Implement `ITenantOwned` for tenant data and `IAuditable` for who/when (ADR-0010, ADR-0012).
- Private constructor, a static `Create` factory, and methods for every change (`Update`, `Rename`, ...).
- Enforce invariants in the entity and throw `DomainException` (mapped to 400). Expose limits as constants
  (`NameMaxLength`) so validators and configuration reuse them.
- Do **not** set `Id`, `TenantId`, `CreatedAt`, `UpdatedAt`, `CreatedBy` or `UpdatedBy`; persistence does.

Unit tests: `tests/Boilerplate.UnitTests/Domain/CustomerTests.cs`, like [`ProjectTests.cs`](../tests/Boilerplate.UnitTests/Domain/ProjectTests.cs).

## 2. Persistence

- Add `DbSet<Customer> Customers` to [`IAppDbContext`](../src/Boilerplate.Application/Common/Persistence/IAppDbContext.cs)
  and [`AppDbContext`](../src/Boilerplate.Infrastructure/Persistence/AppDbContext.cs).
- Add `CustomerConfiguration` like [`ProjectConfiguration.cs`](../src/Boilerplate.Infrastructure/Persistence/Configurations/ProjectConfiguration.cs):
  a length (or `text`) for every string, and indexes with a reason. Unique values of tenant data start with `TenantId`.
- Create the migration and read the generated code:

  ```bash
  dotnet ef migrations add AddCustomers --project src/Boilerplate.Infrastructure --output-dir Persistence/Migrations
  ```

`ModelConventionTests` and `MigrationTests` check the result automatically.

## 3. Permissions

`src/Boilerplate.Application/Features/Customers/CustomerPermissions.cs`, like
[`ProjectPermissions.cs`](../src/Boilerplate.Application/Features/Projects/ProjectPermissions.cs): lowercase
`area.action` names, usually `customers.read` and `customers.manage` (ADR-0017).

Add both constants to [`PermissionCatalog.All`](../src/Boilerplate.Application/Common/Security/PermissionCatalog.cs);
otherwise no role can grant them, and `PermissionCatalogTests` fails (ADR-0022). Administrators get new permissions
automatically.

## 4. Use cases

One folder per use case under `src/Boilerplate.Application/Features/Customers/`, like
[`Features/Projects`](../src/Boilerplate.Application/Features/Projects):

| File | Purpose |
|---|---|
| `<UseCase>Request.cs` | Input record (omit when the only input is an id) |
| `<UseCase>Validator.cs` | Shape and limits with FluentValidation (ADR-0014); list requests derive from `PageRequest` and `Include(new PageRequestValidator())` |
| `<UseCase>Handler.cs` | A plain class with `HandleAsync(..., CancellationToken)`, using `IAppDbContext` |
| `CustomerResponse.cs` (feature level) | Response record plus the `FromCustomer` projection expression (ADR-0015) |

Handler rules:

- Reads: `AsNoTracking()`, `Select(CustomerResponse.FromCustomer)`, and for lists a deterministic order ending with
  the id, then `ToPagedResponseAsync` (ADR-0021).
- Writes: load, call entity methods, one `SaveChangesAsync` (ADR-0011).
- Not found: `throw new NotFoundException("Customer", id)` (404). Duplicate unique value: check first and throw
  `ConflictException` (409); the database index covers races and is translated to 409 too.
- Never filter by tenant; the query filter does it.

Register everything in `CustomersFeature.AddCustomersFeature()` (handlers and `IValidator<T>`, explicitly), like
[`ProjectsFeature.cs`](../src/Boilerplate.Application/Features/Projects/ProjectsFeature.cs), and call it from
[`Application/DependencyInjection.cs`](../src/Boilerplate.Application/DependencyInjection.cs).

Validator unit tests: like [`ProjectValidatorTests.cs`](../tests/Boilerplate.UnitTests/Features/Projects/ProjectValidatorTests.cs).

## 5. Controller

`src/Boilerplate.Api/Controllers/CustomersController.cs`, like
[`ProjectsController.cs`](../src/Boilerplate.Api/Controllers/ProjectsController.cs):

- `[ApiController]`, `[Route("api/v1/customers")]`.
- `[RequirePermission(CustomerPermissions.Read)]` on reads, `...Manage` on writes.
- Each action takes the request, `[FromServices]` handler and `CancellationToken`, and returns the handler's result:
  `200` with the response, `201 CreatedAtRoute` for creation, `204 NoContent` for deletion.
- `[ProducesResponseType]` for every status the action can return, so the OpenAPI document is complete.
- Do not catch exceptions or validate in the controller.

## Sending e-mail or other side effects

Never call an external service from a handler (ADR-0011). Inject `Outbox`, enqueue, and save once:

```csharp
outbox.Enqueue(new EmailMessage(customer.Email, "Welcome", body));
await db.SaveChangesAsync(cancellationToken);   // the e-mail is recorded if and only if the change commits
```

The dispatcher delivers it in the background, at least once, for the tenant that enqueued it (ADR-0019).
For a new kind of side effect, add a message record and an `IOutboxMessageHandler<T>` (idempotent: use
`context.MessageId` to recognise repeats) and register it with
`services.AddOutboxMessage<TMessage, THandler>("stable-name")`. Test it like
[`OutboxProcessorTests`](../tests/Boilerplate.IntegrationTests/Messaging/OutboxProcessorTests.cs).

## 6. Integration tests

`tests/Boilerplate.IntegrationTests/CustomersTests.cs`, like [`ProjectsTests.cs`](../tests/Boilerplate.IntegrationTests/ProjectsTests.cs).
Each test creates its own tenant and user with `AuthorizationData`, so tests never share data. Cover:

- the full create, get, update, delete flow;
- 400 with errors per field, 404, 409;
- paging and ordering for lists;
- another tenant's record → 404 for get, update and delete; lists show only the tenant's data;
- 401 anonymous, 403 without the permission, read permission cannot write.

## 7. Done

```bash
dotnet format --verify-no-changes
dotnet build -c Release
dotnet test -c Release
```

Update the endpoint table in the README.

## Removing the projects feature

Projects exist only as an example. To remove them: delete `Domain/Projects`, `Application/Features/Projects`,
`ProjectConfiguration.cs`, `ProjectsController.cs`, the `Projects` sets, the `AddProjectsFeature()` call and the
project tests, then add a migration that drops the table (`dotnet ef migrations add RemoveProjects ...`).
