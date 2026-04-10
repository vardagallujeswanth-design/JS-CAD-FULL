import { useEffect, useState } from 'react';
import { getEmailSettings, saveEmailSettings, getProviderEmailSettings, saveProviderEmailSettings } from '../services/api';

const defaultEmailSettings = (providerId = 0, applicationId = 0) => ({
  emailSettingId: 0,
  applicationId,
  providerId,
  enabled: false,
  host: '',
  port: 587,
  enableSsl: true,
  fromEmail: '',
  toEmail: '',
  userName: '',
  password: '',
  sendOnSuccess: false,
  sendOnFailure: true,
});

export default function EmailSettings({ application, provider }) {
  const [settings, setSettings] = useState(defaultEmailSettings());
  const isProvider = Boolean(provider);

  useEffect(() => {
    if (isProvider) {
      getProviderEmailSettings(provider.providerId)
        .then((data) => setSettings(data || defaultEmailSettings(provider.providerId)))
        .catch(() => setSettings(defaultEmailSettings(provider.providerId)));
      return;
    }

    if (!application) {
      return;
    }

    getEmailSettings(application.applicationId)
      .then((data) => setSettings({ ...(data || {}), applicationId: application.applicationId }))
      .catch(() => setSettings(defaultEmailSettings(0, application.applicationId)));
  }, [application, isProvider, provider]);

  if (!application && !provider) {
    return (
      <div className="panel">
        <div className="panel-header">
          <h2>Email settings</h2>
        </div>
        <p>Select an application or provider to configure SMTP and notification rules.</p>
      </div>
    );
  }

  const save = async () => {
    try {
      if (isProvider) {
        await saveProviderEmailSettings(provider.providerId, settings);
        window.alert(`Email settings saved for ${provider.providerName}`);
        return;
      }

      await saveEmailSettings(application.applicationId, settings);
      window.alert('Email settings saved.');
    } catch (error) {
      console.error('Failed to save email settings', error);
      window.alert('Unable to save email settings. Check the console for details.');
    }
  };

  const entityName = isProvider ? provider.providerName : application.applicationCode;

  return (
    <>
      <div className="panel-header">
        <h2>Email Settings for {entityName}</h2>
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
            <td>Host</td>
            <td><input value={settings.host} onChange={(e) => setSettings((prev) => ({ ...prev, host: e.target.value }))} /></td>
          </tr>
          <tr>
            <td>Port</td>
            <td><input type="number" value={settings.port} onChange={(e) => setSettings((prev) => ({ ...prev, port: Number(e.target.value) }))} /></td>
          </tr>
          <tr>
            <td>Enable SSL</td>
            <td><input type="checkbox" checked={settings.enableSsl} onChange={(e) => setSettings((prev) => ({ ...prev, enableSsl: e.target.checked }))} /></td>
          </tr>
          <tr>
            <td>From email</td>
            <td><input value={settings.fromEmail} onChange={(e) => setSettings((prev) => ({ ...prev, fromEmail: e.target.value }))} /></td>
          </tr>
          <tr>
            <td>To email</td>
            <td><input value={settings.toEmail} onChange={(e) => setSettings((prev) => ({ ...prev, toEmail: e.target.value }))} /></td>
          </tr>
          <tr>
            <td>Username</td>
            <td><input value={settings.userName} onChange={(e) => setSettings((prev) => ({ ...prev, userName: e.target.value }))} /></td>
          </tr>
          <tr>
            <td>Password</td>
            <td><input type="password" value={settings.password} onChange={(e) => setSettings((prev) => ({ ...prev, password: e.target.value }))} /></td>
          </tr>
          <tr>
            <td>Send on success</td>
            <td><input type="checkbox" checked={settings.sendOnSuccess} onChange={(e) => setSettings((prev) => ({ ...prev, sendOnSuccess: e.target.checked }))} /></td>
          </tr>
          <tr>
            <td>Send on failure</td>
            <td><input type="checkbox" checked={settings.sendOnFailure} onChange={(e) => setSettings((prev) => ({ ...prev, sendOnFailure: e.target.checked }))} /></td>
          </tr>
        </tbody>
      </table>
      <button type="button" onClick={save}>Save email settings</button>
    </>
  );
}
