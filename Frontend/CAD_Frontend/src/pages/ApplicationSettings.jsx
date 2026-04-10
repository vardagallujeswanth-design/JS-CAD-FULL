import { useEffect, useState } from 'react';
import { getApplicationSettings, saveApplicationSettings } from '../services/api';

export default function ApplicationSettings({ application, onNavigate }) {
  const [settings, setSettings] = useState({
    hasSettings: false,
    serviceMode: 'Polling',
    pollIntervalSeconds: 20,
    systemUserId: 0,
    enableParallelPipeline: false,
    logFolder: '',
    maxQueueSize: 0,
    workerCount: 0,
    applicationId: 0,
    additionalSettings: {},
  });

  useEffect(() => {
    if (!application) return;
    getApplicationSettings(application.applicationId)
      .then((data) => setSettings({ ...data, applicationId: application.applicationId, additionalSettings: data.additionalSettings || {} }))
      .catch(() => setSettings({
        hasSettings: false,
        serviceMode: 'Polling',
        pollIntervalSeconds: 20,
        systemUserId: 0,
        enableParallelPipeline: false,
        logFolder: '',
        maxQueueSize: 0,
        workerCount: 0,
        applicationId: application.applicationId,
        additionalSettings: {},
      }));
  }, [application]);

  if (!application) {
    return (
      <section className="page">
        <header className="page-header">
          <h1>Application settings</h1>
          <p>Select an application to manage the application settings.</p>
          <button type="button" className="primary" onClick={() => onNavigate('applications')}>
            Go to Applications
          </button>
        </header>
      </section>
    );
  }

  const save = async () => {
    await saveApplicationSettings(application.applicationId, settings);
    window.alert('Application settings saved.');
  };

  return (
    <section className="page">
      <header className="page-header">
        <h1>Application settings for {application.applicationCode}</h1>
        {!settings.hasSettings && (
          <p style={{ color: '#475569', marginTop: '12px' }}>
            No settings have been saved for this application yet. These are default values.
          </p>
        )}
      </header>
      <div className="panel">
        <h2>Application Settings</h2>
        <table className="settings-table">
          <thead>
            <tr>
              <th>Setting</th>
              <th>Value</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td>Service mode</td>
              <td>
                <select value={settings.serviceMode} onChange={(e) => setSettings((prev) => ({ ...prev, serviceMode: e.target.value }))}>
                  <option value="Polling">Polling</option>
                  <option value="Parallel">Parallel</option>
                </select>
              </td>
              <td></td>
            </tr>
            <tr>
              <td>Enable parallel pipeline</td>
              <td>
                <input type="checkbox" checked={settings.enableParallelPipeline} onChange={(e) => setSettings((prev) => ({ ...prev, enableParallelPipeline: e.target.checked }))} />
              </td>
              <td></td>
            </tr>
            <tr>
              <td>Poll interval (seconds)</td>
              <td>
                <input type="number" value={settings.pollIntervalSeconds} onChange={(e) => setSettings((prev) => ({ ...prev, pollIntervalSeconds: Number(e.target.value) }))} />
              </td>
              <td></td>
            </tr>
            <tr>
              <td>Max queue size</td>
              <td>
                <input type="number" value={settings.maxQueueSize} onChange={(e) => setSettings((prev) => ({ ...prev, maxQueueSize: Number(e.target.value) }))} />
              </td>
              <td></td>
            </tr>
            <tr>
              <td>Worker count</td>
              <td>
                <input type="number" value={settings.workerCount} onChange={(e) => setSettings((prev) => ({ ...prev, workerCount: Number(e.target.value) }))} />
              </td>
              <td></td>
            </tr>
            <tr>
              <td>Log folder</td>
              <td>
                <input value={settings.logFolder} onChange={(e) => setSettings((prev) => ({ ...prev, logFolder: e.target.value }))} />
              </td>
              <td></td>
            </tr>
            <tr>
              <td>System user ID</td>
              <td>
                <input type="number" value={settings.systemUserId} onChange={(e) => setSettings((prev) => ({ ...prev, systemUserId: Number(e.target.value) }))} />
              </td>
              <td>
                <button type="button" onClick={save}>Save settings</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      {settings.additionalSettings && Object.keys(settings.additionalSettings).length > 0 && (
        <div className="panel">
          <h2>Additional loaded settings</h2>
          <table>
            <thead>
              <tr>
                <th>Setting</th>
                <th>Value</th>
              </tr>
            </thead>
            <tbody>
              {Object.entries(settings.additionalSettings).map(([key, value]) => (
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
