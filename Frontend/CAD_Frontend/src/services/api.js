const baseUrl = import.meta.env.VITE_API_BASE_URL ?? '';

async function fetchJson(path, options = {}) {
  const response = await fetch(`${baseUrl}${path}`, {
    headers: {
      'Content-Type': 'application/json',
    },
    ...options,
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(`${response.status} ${response.statusText}: ${text}`);
  }

  if (response.status === 204) return null;
  try {
    return await response.json();
  } catch {
    return null;
  }
}

export const getApplications = () => fetchJson('/api/applications');
export const saveApplication = (application) => fetchJson('/api/applications', {
  method: application.applicationId ? 'PUT' : 'POST',
  body: JSON.stringify(application),
});
export const deleteApplication = (id) => fetchJson(`/api/applications/${id}`, { method: 'DELETE' });

export const getApplicationSettings = (applicationId) => fetchJson(`/api/applications/${applicationId}/settings`);
export const saveApplicationSettings = (applicationId, settings) => fetchJson(`/api/applications/${applicationId}/settings`, {
  method: 'PUT',
  body: JSON.stringify(settings),
});

export const getProviders = (applicationId) => fetchJson(`/api/applications/${applicationId}/providers`);
export const saveProvider = (provider) => fetchJson('/api/providers', {
  method: provider.providerId ? 'PUT' : 'POST',
  body: JSON.stringify(provider),
});
export const deleteProvider = (id) => fetchJson(`/api/providers/${id}`, { method: 'DELETE' });

export const getProviderFolders = (providerId) => fetchJson(`/api/providers/${providerId}/folders`);
export const saveProviderFolders = (providerId, folders) => fetchJson(`/api/providers/${providerId}/folders`, {
  method: 'PUT',
  body: JSON.stringify(folders),
});

export const getProviderProcedures = (providerId) => fetchJson(`/api/providers/${providerId}/procedures`);
export const saveProviderProcedure = (providerId, procedure) => fetchJson(`/api/providers/${providerId}/procedures`, {
  method: procedure.procedureId ? 'PUT' : 'POST',
  body: JSON.stringify(procedure),
});
export const deleteProviderProcedure = (providerId, procedureId) => fetchJson(`/api/providers/${providerId}/procedures/${procedureId}`, { method: 'DELETE' });

export const getFieldMappings = (providerId) => fetchJson(`/api/providers/${providerId}/field-mappings`);
export const saveFieldMapping = (providerId, mapping) => fetchJson(`/api/providers/${providerId}/field-mappings`, {
  method: mapping.mappingId ? 'PUT' : 'POST',
  body: JSON.stringify(mapping),
});
export const deleteFieldMapping = (providerId, mappingId) => fetchJson(`/api/providers/${providerId}/field-mappings/${mappingId}`, { method: 'DELETE' });

export const getProviderRules = (providerId) => fetchJson(`/api/providers/${providerId}/rules`);
export const saveProviderRule = (providerId, rule) => fetchJson(`/api/providers/${providerId}/rules`, {
  method: rule.ruleId ? 'PUT' : 'POST',
  body: JSON.stringify(rule),
});
export const deleteProviderRule = (providerId, ruleId) => fetchJson(`/api/providers/${providerId}/rules/${ruleId}`, { method: 'DELETE' });

export const getRetrySettings = (applicationId) => fetchJson(`/api/applications/${applicationId}/retry-settings`);
export const saveRetrySettings = (applicationId, settings) => fetchJson(`/api/applications/${applicationId}/retry-settings`, {
  method: 'PUT',
  body: JSON.stringify(settings),
});

export const getProviderRetrySettings = (providerId) => fetchJson(`/api/providers/${providerId}/retry-settings`);
export const saveProviderRetrySettings = (providerId, settings) => fetchJson(`/api/providers/${providerId}/retry-settings`, {
  method: 'PUT',
  body: JSON.stringify(settings),
});

export const getEmailSettings = (applicationId) => fetchJson(`/api/applications/${applicationId}/email-settings`);
export const saveEmailSettings = (applicationId, settings) => fetchJson(`/api/applications/${applicationId}/email-settings`, {
  method: 'PUT',
  body: JSON.stringify(settings),
});

export const getProviderEmailSettings = (providerId) => fetchJson(`/api/providers/${providerId}/email-settings`);
export const saveProviderEmailSettings = (providerId, settings) => fetchJson(`/api/providers/${providerId}/email-settings`, {
  method: 'PUT',
  body: JSON.stringify(settings),
});

export const getServiceMetaData = (applicationId) => fetchJson(`/api/applications/${applicationId}/metadata`);
export const saveServiceMetaData = (applicationId, metadata) => fetchJson(`/api/applications/${applicationId}/metadata`, {
  method: 'PUT',
  body: JSON.stringify(metadata),
});

export const getProviderServiceMetaData = (providerId) => fetchJson(`/api/providers/${providerId}/metadata`);
export const saveProviderServiceMetaData = (providerId, metadata) => fetchJson(`/api/providers/${providerId}/metadata`, {
  method: 'PUT',
  body: JSON.stringify(metadata),
});

export const getProviderServiceMetadataRows = (providerId) => fetchJson(`/api/providers/${providerId}/service-metadata`);
export const saveProviderServiceMetadataRow = (providerId, metadata) => fetchJson(`/api/providers/${providerId}/service-metadata`, {
  method: metadata.cdServiceMetaDataId ? 'PUT' : 'POST',
  body: JSON.stringify(metadata),
});
export const deleteProviderServiceMetadata = (providerId, metadataId) => fetchJson(`/api/providers/${providerId}/service-metadata/${metadataId}`, {
  method: 'DELETE',
});
