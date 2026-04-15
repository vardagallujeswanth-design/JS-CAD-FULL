import { useEffect, useState } from 'react';

export default function ProviderForm({ application, provider, onSave, onCancel }) {
  const [draft, setDraft] = useState({
    providerCode: '',
    identificationPath: '',
    identifierValue: '',
    callerNameNode: '',
    callNumberNode: '',
    primaryOfficerNameNode: '',
    officersNode: '',
  });

  useEffect(() => {
    if (!provider) {
      setDraft({
        providerCode: '',
        identificationPath: '',
        identifierValue: '',
        callerNameNode: '',
        callNumberNode: '',
        primaryOfficerNameNode: '',
        officersNode: '',
      });
      return;
    }

    setDraft({
      providerId: provider.providerId,
      providerCode: provider.providerCode || '',
      identificationPath: provider.identificationPath || '',
      identifierValue: provider.identifierValue || '',
      callerNameNode: provider.callerNameNode || '',
      callNumberNode: provider.callNumberNode || '',
      primaryOfficerNameNode: provider.primaryOfficerNameNode || '',
      officersNode: provider.officersNode || '',
    });
  }, [provider]);

  if (!application) {
    return (
      <section className="page">
        <header className="page-header">
          <h1>Provider form</h1>
          <p>Select an application first to add or edit provider metadata.</p>
        </header>
      </section>
    );
  }

  const handleSubmit = async (event) => {
    event.preventDefault();
    await onSave({ ...draft, applicationId: application.applicationId });
  };

  return (
    <section className="page">
      <header className="page-header">
        <h1>{provider ? 'Edit provider' : 'Add provider'}</h1>
        <p>{provider ? 'Update the provider data' : 'Create a new provider using the create provider button.'}</p>
      </header>

      <div className="panel stacked-form">
        <form onSubmit={handleSubmit}>
          <label>
            Provider code
            <input
              value={draft.providerCode}
              onChange={(e) => setDraft((prev) => ({ ...prev, providerCode: e.target.value }))}
              required
            />
          </label>
          <label>
            Identification path
            <input
              value={draft.identificationPath}
              onChange={(e) => setDraft((prev) => ({ ...prev, identificationPath: e.target.value }))}
            />
          </label>
          <label>
            Identifier value
            <input
              value={draft.identifierValue}
              onChange={(e) => setDraft((prev) => ({ ...prev, identifierValue: e.target.value }))}
            />
          </label>
          <label>
            Caller name node
            <input
              value={draft.callerNameNode}
              onChange={(e) => setDraft((prev) => ({ ...prev, callerNameNode: e.target.value }))}
            />
          </label>
          <label>
            Call number node
            <input
              value={draft.callNumberNode}
              onChange={(e) => setDraft((prev) => ({ ...prev, callNumberNode: e.target.value }))}
            />
          </label>
          <label>
            Primary officer node
            <input
              value={draft.primaryOfficerNameNode}
              onChange={(e) => setDraft((prev) => ({ ...prev, primaryOfficerNameNode: e.target.value }))}
            />
          </label>
          <label>
            Officers node
            <input
              value={draft.officersNode}
              onChange={(e) => setDraft((prev) => ({ ...prev, officersNode: e.target.value }))}
            />
          </label>

          <div className="form-actions">
            <button type="submit">Save</button>
            <button type="button" className="secondary" onClick={onCancel}>
              Cancel
            </button>
          </div>
        </form>
      </div>
    </section>
  );
}
