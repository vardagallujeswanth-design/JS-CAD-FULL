# CAD Processing Service

A combined backend and frontend repository for managing CAD provider settings, application metadata, folder paths, procedures, rules, email settings, and retry configuration.

## Project overview

This repository includes:

- **Backend**: `.NET` service in the root folder using `CadProcessorService.csproj`.
- **Frontend**: React + Vite administration tool in `Frontend/CAD_Frontend`.
- **Configuration**: `appsettings.json` at the root controls backend settings.

The backend exposes APIs for provider metadata, application definitions, field mappings, rules, service metadata, email settings, and retry settings.

## Architecture

- `CadProcessorService.csproj` contains the main service and dependencies.
- `Program.cs` launches the backend service.
- Controllers expose API endpoints for the frontend.
- Models define the domain objects used across the service.
- Helpers and Infrastructure provide shared utilities and database access.
- `Frontend/CAD_Frontend` contains the web administration UI.

## Frontend application

The frontend is a React application powered by Vite.

Key features:

- Manage applications and providers
- Edit provider folder paths and processing rules
- Configure field mappings, email settings, and retry behavior
- Supports application settings and provider detail workflows

Important files:

- `Frontend/CAD_Frontend/package.json` - frontend dependencies and scripts
- `Frontend/CAD_Frontend/src/App.jsx` - main application shell and navigation
- `Frontend/CAD_Frontend/src/pages/` - page components
- `Frontend/CAD_Frontend/src/components/Sidebar.jsx` - navigation sidebar
- `Frontend/CAD_Frontend/src/App.css` and `src/index.css` - UI styling

## Repository structure

- `CadProcessorService.csproj` - backend project file
- `CadProcessorService.sln` - Visual Studio solution
- `Program.cs` - backend entry point
- `Controllers/` - backend API controllers
- `Helpers/`, `Infrastructure/`, `Models/`, `Services/` - backend layers
- `Frontend/CAD_Frontend/` - React frontend
- `appsettings.json` - runtime configuration
- `ApiTestProviderFolders.js`, `check_save_folders.js` - helper scripts

## Setup and local development

### Backend

1. Open the root folder in Visual Studio or use the .NET CLI.
2. Restore packages:

```bash
cd "c:\vardagallu_jeswanth\CAD_NEWV\CAD_NEWV2 - dup\CAD_SourceCode_v2"
dotnet restore
```

3. Build the backend:

```bash
dotnet build
```

4. Run the backend:

```bash
dotnet run
```

The backend will start and serve API endpoints for the frontend.

### Frontend

1. Install Node dependencies:

```bash
cd "c:\vardagallu_jeswanth\CAD_NEWV\CAD_NEWV2 - dup\CAD_SourceCode_v2\Frontend\CAD_Frontend"
npm install
```

2. Start the development server:

```bash
npm run dev
```

3. Build the production bundle:

```bash
npm run build
```

## Common commands

### Backend

- `dotnet restore`
- `dotnet build`
- `dotnet run`

### Frontend

- `npm install`
- `npm run dev`
- `npm run build`
- `npm run lint`

## Notes and recommendations

- Use separate terminals for backend and frontend while developing.
- Frontend changes are hot-reloaded by Vite.
- Backend debugging is easiest through Visual Studio.
- Ensure `appsettings.json` is configured before running the backend.

## What to customize

- Backend: update `appsettings.json` for environment-specific settings
- Frontend: edit `Frontend/CAD_Frontend/src/` for UI and API behavior
- Add repository-specific documentation if you connect to a real database or service

## Troubleshooting

- If the frontend fails to start, verify Node and npm versions and run `npm install` again.
- If the backend does not start, check `appsettings.json` and restore NuGet packages.

## License

This repository does not include a specific license file. Add a `LICENSE` if you want to make reuse terms explicit.
