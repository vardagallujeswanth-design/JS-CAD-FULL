import { useEffect, useState } from 'react';
import { getProviderServiceMetaData, saveProviderServiceMetaData } from '../services/api';

export default function ServiceMetadata({ provider, onNavigate }) {
  const [metadata, setMetadata] = useState({ serviceName: '', serviceMode: '', description: '', additionalSettings: {} });

  useEffect(() => {
    if (!provider) return;
    getProviderServiceMetaData(provider.providerId)
      .then((data) => setMetadata({ ...data, additionalSettings: data.additionalSettings || {} }))
      .catch(() => setMetadata({ serviceName: '', serviceMode: '', description: '', additionalSettings: {} }));
  }, [provider]);

  if (!provider) {
    return (
      <section className="page">
        <header className="page-header">
          <h1>Service metadata</h1>
          <p>Select a provider to view service metadata and feature flags.</p>
          <button type="button" className="primary" onClick={() => onNavigate('providers')}>
            Go to Providers
          </button>
        </header>
      </section>
    );
  }

  const save = async () => {
    await saveProviderServiceMetaData(provider.providerId, metadata);
    window.alert('Service metadata saved.');
  };

  return (
    <section className="page">
      <header className="page-header">
        <h1>Service metadata for {provider.providerName}</h1>
      </header>
      <div className="panel stacked-form">
        <label>
          Service name
          <input value={metadata.serviceName} onChange={(e) => setMetadata((prev) => ({ ...prev, serviceName: e.target.value }))} />
        </label>
        <label>
          Service mode
          <input value={metadata.serviceMode} onChange={(e) => setMetadata((prev) => ({ ...prev, serviceMode: e.target.value }))} />
        </label>
        <label>
          Description
          <textarea value={metadata.description} onChange={(e) => setMetadata((prev) => ({ ...prev, description: e.target.value }))} />
        </label>
        <button type="button" onClick={save}>Save metadata</button>
      </div>
      {metadata.additionalSettings && Object.keys(metadata.additionalSettings).length > 0 && (
        <div className="panel">
          <h2>Additional loaded metadata</h2>
          <table>
            <thead>
              <tr>
                <th>Setting</th>
                <th>Value</th>
              </tr>
            </thead>
            <tbody>
              {Object.entries(metadata.additionalSettings).map(([key, value]) => (
                <tr key={key}>
                  <td>{key}</td>
                  <td>{value}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
