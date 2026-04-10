export default function Dashboard({ application, provider }) {
  return (
    <section className="page">
      <header className="page-header">
        <h1>CAD Settings Dashboard</h1>
        <p>Manage the application and provider settings.</p>
      </header>

      <div className="cards-grid">
        <div className="card">
          <h2>Selected Application</h2>
          <p>{application ? application.applicationCode : 'None selected'}</p>
        </div>
        <div className="card">
          <h2>Selected Provider</h2>
          <p>{provider ? provider.providerCode : 'None selected'}</p>
        </div>
        <div className="card">
          <h2>Next step</h2>
          <p>Select an application, then select required provider , then manage folder paths, procedures, mappings, email settings, retry settings and rules.</p>
        </div>
      </div>
    </section>
  );
}
