# CAD Processing Service

This repository contains a CAD processing system with a .NET backend service and a React/Vite frontend administration console.

## Project overview

- **Backend**: `CadProcessorService` - a .NET application built from `CadProcessorService.csproj`.
- **Frontend**: `Frontend/CAD_Frontend` - a React + Vite application for managing applications, providers, rules, and service settings.
- **Configuration**: `appsettings.json` lives at the repository root and contains backend configuration values.

## Repository structure

- `CadProcessorService.csproj` - main backend project file.
- `CadProcessorService.sln` - Visual Studio solution file.
- `Program.cs` - service entry point.
- `Controllers/` - API controllers.
- `Helpers/`, `Infrastructure/`, `Models/`, `Services/` - backend service layers.
- `Frontend/CAD_Frontend/` - frontend application.
- `Frontend/CAD_Frontend/src/` - React source code.
- `Frontend/CAD_Frontend/package.json` - frontend dependencies and scripts.

## Backend setup

1. Open the solution in Visual Studio or use the .NET CLI.
2. Restore packages:

```bash
cd "c:\vardagallu_jeswanth\CAD_NEWV\CAD_NEWV2 - dup\CAD_SourceCode_v2"
dotnet restore
```

3. Build and run the backend:

```bash
dotnet build
dotnet run
```

4. Configuration is loaded from `appsettings.json`.

## Frontend setup

1. Install dependencies:

```bash
cd "c:\vardagallu_jeswanth\CAD_NEWV\CAD_NEWV2 - dup\CAD_SourceCode_v2\Frontend\CAD_Frontend"
npm install
```

2. Run the development server:

```bash
npm run dev
```

3. Build for production:

```bash
npm run build
```

## Notes

- The frontend uses React 19 with Vite.
- The backend is a .NET application that appears to support Windows runtime scenarios.
- The project is organized so backend and frontend work independently but share the same repository.

## Recommended workflow

- Keep backend and frontend running separately during development.
- Use Visual Studio for backend debugging.
- Use `npm run dev` to preview frontend changes quickly.
