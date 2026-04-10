# Provider-Specific Settings Refactoring

## Overview
Refactored email and retry settings from **application-level** to **provider-level**, enabling each provider (HSCO, TylerCAD, etc.) to have independent configurations.

## Changes Made

### 1. Data Models (Models/)
- **DbEmailConfig.cs**: Added `ProviderId` field
- **DbRetryConfig.cs**: Added `ProviderId` field

Both models now store:
- `ApplicationId` (for reference)
- `ProviderId` (new - enables per-provider settings)

### 2. Database Layer (Infrastructure/DatabaseExecutor.cs)

#### New Methods:
```csharp
// Retry Settings
- GetProviderRetrySettings(int providerId)
- SaveProviderRetrySettings(int providerId, DbRetryConfig settings)

// Email Settings
- GetProviderEmailSettings(int providerId)
- SaveProviderEmailSettings(int providerId, DbEmailConfig settings)
```

These methods:
- Query by `ProviderId` instead of `ApplicationId`
- Return provider-specific configurations with fallbacks
- Support upsert operations (insert if new, update if exists)

### 3. API Endpoints (Controllers/ProvidersController.cs)

**New Provider-Specific Routes:**
```
GET    /api/providers/{providerId}/email-settings
PUT    /api/providers/{providerId}/email-settings
GET    /api/providers/{providerId}/retry-settings
PUT    /api/providers/{providerId}/retry-settings
```

**Legacy Endpoints (Still Available):**
```
GET    /api/applications/{applicationId}/email-settings
PUT    /api/applications/{applicationId}/email-settings
GET    /api/applications/{applicationId}/retry-settings
PUT    /api/applications/{applicationId}/retry-settings
```

### 4. Worker Service (Services/Worker.cs)

**Changes:**
- Added `_providerRetryConfigs` dictionary to cache provider-specific settings
- Updated `RefreshCache()` to load retry settings for each provider
- Added `GetRetryConfigForProvider(DbProviderConfig provider)` helper method
- Updated all retry logic to use provider-specific settings:
  - `ProcessFile()`: Uses provider-specific retry config
  - `HandleResult()`: Uses provider-specific retry config
  - `EnqueueRetry()`: Uses provider-specific delay
  - `ProcessRetryQueue()`: Uses provider-specific max attempts & delays

### 5. Email Service (Services/EmailService.cs)

**New Overloads:**
```csharp
- SendFailure(int providerId, string file, string message)
- SendSuccess(int providerId, string file, string message)
- GetConfigForProvider(int providerId)
```

**Behavior:**
- Maintains backward-compatible application-level methods
- New methods load provider-specific email configs
- Falls back to application config if provider config not found

## Benefits

✅ **Provider Isolation**: Each provider can have different retry policies  
✅ **Email Customization**: HSCO can email one address, TylerCAD another  
✅ **Flexible Retry Strategy**: Different providers may need different retry delays  
✅ **Backward Compatible**: Existing application-level settings still work  
✅ **Scalable**: Easy to add more providers with independent configs  

## Example Usage

**API Usage:**
```bash
# Get HSCO email settings
GET /api/providers/1/email-settings

# Update HSCO retry settings
PUT /api/providers/1/retry-settings
{
  "enabled": true,
  "maxAttempts": 5,
  "delaySeconds": 60
}

# Get TylerCAD email settings
GET /api/providers/2/email-settings
```

**Frontend Usage:**
```javascript
// Load provider-specific settings
const emailSettings = await api.getProviderEmailSettings(providerId);
const retrySettings = await api.getProviderRetrySettings(providerId);

// Save provider-specific settings
await api.saveProviderEmailSettings(providerId, {
  enabled: true,
  host: 'smtp.example.com',
  port: 587,
  // ... other settings
});
```

## Database Requirements

The existing tables (`CAD_EmailSettings`, `CAD_RetrySettings`) support both `ApplicationId` and `ProviderId`.

**Migration (if needed):**
```sql
ALTER TABLE CAD_EmailSettings
ADD ProviderId INT NOT NULL DEFAULT 0;

ALTER TABLE CAD_RetrySettings
ADD ProviderId INT NOT NULL DEFAULT 0;

-- Create indexes for faster lookups
CREATE INDEX IX_EmailSettings_ProviderId ON CAD_EmailSettings(ProviderId);
CREATE INDEX IX_RetrySettings_ProviderId ON CAD_RetrySettings(ProviderId);
```

## Testing Checklist

- [ ] Test GET /api/providers/{id}/email-settings
- [ ] Test PUT /api/providers/{id}/email-settings
- [ ] Test GET /api/providers/{id}/retry-settings
- [ ] Test PUT /api/providers/{id}/retry-settings
- [ ] Verify retry logic uses provider settings during error processing
- [ ] Test fallback when provider settings don't exist
- [ ] Verify backward compatibility with application-level settings
- [ ] Test email sending with provider-specific configs

## Files Modified

1. `Models/DbEmailConfig.cs`
2. `Models/DbRetryConfig.cs`
3. `Infrastructure/DatabaseExecutor.cs`
4. `Controllers/ProvidersController.cs`
5. `Services/Worker.cs`
6. `Services/EmailService.cs`

## Compilation Status

✅ Build succeeded (0 errors, 2 warnings)
