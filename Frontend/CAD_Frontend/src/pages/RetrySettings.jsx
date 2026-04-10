import { useEffect, useState } from 'react';
import { getRetrySettings, saveRetrySettings, getProviderRetrySettings, saveProviderRetrySettings } from '../services/api';

const defaultRetrySettings = (providerId = 0, applicationId = 0) => ({
  retrySettingId: 0,
  applicationId,
  providerId,
  enabled: false,
  maxAttempts: 3,
  delaySeconds: 30,
});

export default function RetrySettings({ application, provider }) {
  const [settings, setSettings] = useState(defaultRetrySettings());
  const isProvider = Boolean(provider);

  useEffect(() => {
    if (isProvider) {
      getProviderRetrySettings(provider.providerId)
        .then((data) => setSettings(data || defaultRetrySettings(provider.providerId)))
        .catch(() => setSettings(defaultRetrySettings(provider.providerId)));
      return;
    }

    if (!application) {
      return;
    }

    getRetrySettings(application.applicationId)
      .then((data) => setSettings({ ...(data || {}), applicationId: application.applicationId }))
      .catch(() => setSettings(defaultRetrySettings(0, application.applicationId)));
  }, [application, isProvider, provider]);

  if (!application && !provider) {
    return (
      <div className="panel">
        <div className="panel-header">
          <h2>Retry settings</h2>
        </div>
        <p>Select an application or provider to configure retry behavior.</p>
      </div>
    );
  }

  const save = async () => {
    try {
      if (isProvider) {
        await saveProviderRetrySettings(provider.providerId, settings);
        window.alert(`Retry settings saved for ${provider.providerName}`);
        return;
      }

      await saveRetrySettings(application.applicationId, settings);
      window.alert('Retry settings saved.');
    } catch (error) {
      console.error('Failed to save retry settings', error);
      window.alert('Unable to save retry settings. Check the console for details.');
    }
  };

  const entityName = isProvider ? provider.providerName : application.applicationCode;

  return (
    <>
      <div className="panel-header">
        <h2>Retry Settings for {entityName}</h2>
      </div>
      <table className="settings-table">
        <thead>
          <tr>
            <th>Setting</th>
            <th>Value</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <td>Enabled</td>
            <td><input type="checkbox" checked={settings.enabled} onChange={(e) => setSettings((prev) => ({ ...prev, enabled: e.target.checked }))} /></td>
          </tr>
          <tr>
            <td>Max attempts</td>
            <td><input type="number" value={settings.maxAttempts} onChange={(e) => setSettings((prev) => ({ ...prev, maxAttempts: Number(e.target.value) }))} /></td>
          </tr>
          <tr>
            <td>Delay seconds</td>
            <td><input type="number" value={settings.delaySeconds} onChange={(e) => setSettings((prev) => ({ ...prev, delaySeconds: Number(e.target.value) }))} /></td>
          </tr>
        </tbody>
      </table>
      <button type="button" onClick={save}>Save retry settings</button>
    </>
  );
}
