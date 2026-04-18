import { useEffect, useState } from 'react';

export default function Applications({ applications, selectedApplication, onSelect, onSave, onDelete }) {
  const [draft, setDraft] = useState({ applicationCode: '', applicationName: '' });

  useEffect(() => {
    if (!selectedApplication) return;
    setDraft({
      applicationCode: selectedApplication.applicationCode || '',
      applicationName: selectedApplication.applicationName || '',
      applicationId: selectedApplication.applicationId,
    });
  }, [selectedApplication]);

  const handleSubmit = (e) => {
    e.preventDefault();
    onSave(draft);
    setDraft({ applicationCode: '', applicationName: '' });
  };

  return (
    <section className="page">
      <header className="page-header">
        <div>
          <h1>Applications</h1>
          <p>Manage the application settings used by the file processing service</p>
          <h2>Select the Application</h2>
        </div>
      </header>

      <div className="page-grid">
        <div className="panel">
          <h2>Application list</h2>
          <table>
            <thead>
              <tr>
                <th>Code</th>
                <th>Name</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              {applications.length === 0 ? (
                <tr>
                  <td colSpan="3" style={{ color: '#9ca3af', fontStyle: 'italic' }}>No applications found.</td>
                </tr>
              ) : (
                applications.map((app) => (
                  <tr
                    key={app.applicationId}
                    className={selectedApplication?.applicationId === app.applicationId ? 'selected-row' : ''}
                  >
                    <td style={{ fontWeight: 500 }}>{app.applicationCode}</td>
                    <td style={{ color: '#6b7280' }}>{app.applicationName || '—'}</td>
                    <td>
                      <div className="action-buttons">
                        <button type="button" className="primary" onClick={() => onSelect(app)}>Select</button>
                        <button type="button" className="danger" onClick={() => onDelete(app.applicationId)}>Delete</button>
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        <div className="panel">
          <h2>{draft.applicationId ? 'Edit application' : 'New application'}</h2>
          <form onSubmit={handleSubmit} className="stacked-form">
            <label>
              Code
              <input
                value={draft.applicationCode}
                onChange={(e) => setDraft((p) => ({ ...p, applicationCode: e.target.value }))}
                placeholder="Application Code"
                required
              />
            </label>
            <label>
              Name
              <input
                value={draft.applicationName}
                onChange={(e) => setDraft((p) => ({ ...p, applicationName: e.target.value }))}
                placeholder="Application Name"
              />
            </label>
            <div className="form-actions">
              <button type="submit" className="primary">Save</button>
              <button type="button" onClick={() => setDraft({ applicationCode: '', applicationName: '' })}>Reset</button>
            </div>
          </form>
        </div>
      </div>
      {selectedApplication && (
  <footer>
    <h2>
      Select the Provider for {selectedApplication.applicationName}
    </h2>
  </footer>
)}
    </section>
  );
}