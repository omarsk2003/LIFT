# LIFT

LIFT is a web platform for fitness and nutrition coaching. Clients can track their progress, communicate with coaches and nutrition specialists, and receive AI-assisted workout and nutrition plan drafts. Specialists review those drafts before they are published. An admin workspace manages accounts, applications, subscriptions, promo codes, and support requests.

## Technology stack

| Area | Technology |
| --- | --- |
| Backend | ASP.NET Core MVC on .NET 10, C# |
| Frontend | Razor Views, HTML, CSS, JavaScript |
| Database | SQL Server with Entity Framework Core |
| Authentication | ASP.NET Core cookie authentication and role-based authorization |
| AI | Ollama models accessed through the application service layer |
| Payments | PayPal Checkout integration in Sandbox mode |

## Main features

- **Client:** registration, profile, subscription, progress tracking, messaging, and AI-assisted plan requests.
- **Coach and nutrition specialist:** certificate-based registration, assigned clients, messaging, plan editing, and review of AI-generated drafts.
- **Admin:** account and application review, specialist change requests, subscription prices, promo codes, support messages, and dashboard reports.

AI-generated workout and nutrition plans are validated by the application and sent to the assigned specialist for review. The AI does not publish plans directly to clients.

## Run locally

1. Install the .NET 10 SDK and SQL Server.
2. Configure `ConnectionStrings:DefaultConnection` for your local database. Keep credentials in .NET user secrets or local environment configuration, outside source control.
3. Restore and run the MVC project from the repository root:

   ```powershell
   dotnet restore LIFT/LIFT.csproj
   dotnet run --project LIFT/LIFT.csproj
   ```

You can also open `LIFT.slnx` in Visual Studio. Development launch settings are in `LIFT/Properties/launchSettings.json`.

## Project structure

| Path | Purpose |
| --- | --- |
| `LIFT/Controllers/` | MVC endpoints and request handling |
| `LIFT/Services/` | Application and integration logic |
| `LIFT/Models/` | Domain and view models |
| `LIFT/Data/` | Entity Framework Core database context |
| `LIFT/Views/` | Razor pages for each workspace |
| `LIFT/wwwroot/` | Styles, scripts, images, and other static assets |

## Development status

The project is under active development. PayPal is configured for Sandbox testing; live billing and automatic renewal are not complete. Some frontend preview pages are still being migrated to database-backed MVC flows. Kaggle datasets and Google Colab are being considered for future AI evaluation; they are not part of the current production AI pipeline.
