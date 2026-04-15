import { useEffect, useState } from 'react';
import './App.css';
import './components/Sidebar.css';
import Sidebar from './components/Sidebar';
import Dashboard from './pages/Dashboard';
import Applications from './pages/Applications';
import Providers from './pages/Providers';
import ProviderDetail from './pages/ProviderDetail';
import RetrySettings from './pages/RetrySettings';
import EmailSettings from './pages/EmailSettings';
import ApplicationSettings from './pages/ApplicationSettings';
import ProviderForm from './pages/ProviderForm';
import {
  getApplications,
  saveApplication,
  deleteApplication,
  getProviders,
  saveProvider,
  deleteProvider,
} from './services/api';

function App() {
  const [view, setView] = useState('applications');
  const [applications, setApplications] = useState([]);
  const [providers, setProviders] = useState([]);
  const [application, setApplication] = useState(null);
  const [provider, setProvider] = useState(null);
  const [editProvider, setEditProvider] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const loadApplications = async () => {
    setLoading(true);
    setError('');
    try {
      const data = await getApplications();
      setApplications(data || []);
    } catch (err) {
      setError('Unable to load applications.');
    } finally {
      setLoading(false);
    }
  };

  const loadProviders = async (appId) => {
    if (!appId) return;
    setLoading(true);
    setError('');
    try {
      const data = await getProviders(appId);
      setProviders(data || []);
    } catch (err) {
      setProviders([]);
      setError('Unable to load providers.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadApplications();
  }, []);

  useEffect(() => {
    if (!application) {
      setProviders([]);
      setProvider(null);
      return;
    }

    loadProviders(application.applicationId);
  }, [application]);

  const handleSaveApplication = async (app) => {
    setLoading(true);
    setError('');
    try {
      const saved = await saveApplication(app);
      await loadApplications();
      if (saved && saved.applicationId) {
        setApplication({
          ...app,
          applicationId: saved.applicationId,
          applicationName: saved.applicationName ?? app.applicationName,
        });
      } else if (app.applicationId && application?.applicationId === app.applicationId) {
        setApplication(app);
      }
    } catch (err) {
      setError('Could not save application.');
    } finally {
      setLoading(false);
    }
  };

  const handleDeleteApplication = async (id) => {
    if (!window.confirm('Delete this application?')) return;
    setLoading(true);
    setError('');
    try {
      await deleteApplication(id);
      if (application?.applicationId === id) setApplication(null);
      await loadApplications();
    } catch (err) {
      setError('Could not delete application.');
    } finally {
      setLoading(false);
    }
  };

const handleSaveProvider = async (prov) => {
  if (!application) return;
  setLoading(true);
  setError('');
  try {
    const saved = await saveProvider({ ...prov, applicationId: application.applicationId });
    await loadProviders(application.applicationId);
    setEditProvider(null);
    setView('providers');

    if (saved && saved.providerId) {
     
      setProvider(saved);
    } else if (prov.providerId) {
     
      setProvider({ ...prov, applicationId: application.applicationId });
    }
    
  } catch (err) {
    setError('Could not save provider.');
  } finally {
    setLoading(false);
  }
};

  const handleDeleteProvider = async (id) => {
    if (!window.confirm('Delete this provider?')) return;
    setLoading(true);
    setError('');
    try {
      await deleteProvider(id);
      if (provider?.providerId === id) setProvider(null);
      setEditProvider((prev) => (prev?.providerId === id ? null : prev));
      if (application) {
        await loadProviders(application.applicationId);
      }
    } catch (err) {
      setError('Could not delete provider.');
    } finally {
      setLoading(false);
    }
  };

  const handleProviderSelect = (prov) => {
  setProvider(prov);
  setView('provider-detail');
};

  const handleProviderSelectById = (providerId) => {
  const selected = providers.find(
    (prov) => prov.providerId === Number(providerId)
  );
  setProvider(selected || null);
  setView('provider-detail');
};

  const handleEditProvider = (prov) => {
    setEditProvider(prov);
    setView('provider-edit');
  };

  const handleAddProvider = () => {
    setEditProvider(null);
    setView('provider-add');
  };

  const renderContent = () => {
    if (loading) {
      return <div className="status-banner">Loading…</div>;
    }

    if (error) {
      return <div className="status-banner error">{error}</div>;
    }

    switch (view) {
      case 'applications':
        return (
          <Applications
            applications={applications}
            selectedApplication={application}
            onSelect={setApplication}
            onSave={handleSaveApplication}
            onDelete={handleDeleteApplication}
          />
        );
      case 'provider-add':
        return (
          <ProviderForm
            application={application}
            provider={null}
            onSave={handleSaveProvider}
            onCancel={() => setView('providers')}
          />
        );
      case 'provider-edit':
        return (
          <ProviderForm
            application={application}
            provider={editProvider}
            onSave={handleSaveProvider}
            onCancel={() => setView('providers')}
          />
        );
  case 'providers':
  return (
    <Providers
      application={application}
      providers={providers}
      selectedProvider={provider}
      onSelect={handleProviderSelect}
      onEdit={handleEditProvider}
      onAdd={handleAddProvider}
      onDelete={handleDeleteProvider}
    />
  );
  case 'provider-detail':
  return (
    <div className="provider-detail-page">
      <button
        type="button"
        className="back-button"
        onClick={() => setView('providers')}
      >
        ← Back to providers
      </button>
      {provider && (
        <div className="provider-profile">
          <div className="provider-profile__avatar">
            {provider.providerCode?.slice(0,2).toUpperCase()}
          </div>
          <div className="provider-profile__info">
            <h2>{provider.providerCode}</h2>
            <p>Folders · Procedures · Field mappings · Rules · Notifications</p>
          </div>
          <div className="provider-profile__actions">
            <button type="button" className="secondary" onClick={() => handleEditProvider(provider)}>
              Edit provider
            </button>
            <button type="button" className="danger" onClick={() => handleDeleteProvider(provider.providerId)}>
              Delete
            </button>
          </div>
        </div>
      )}
      <ProviderDetail application={application} provider={provider} />
    </div>
  );
      case 'retry':
        return <RetrySettings application={application} provider={provider} />;
      case 'email':
        return <EmailSettings application={application} provider={provider} />;
      case 'settings':
        return <ApplicationSettings application={application} onNavigate={setView} />;
      default:
        return <Dashboard application={application} provider={provider} />;
    }
  };

return (
  <div className="app-shell">
    <Sidebar active={view} onNavigate={setView} />
    <div className="main-area">
      <header className="app-toolbar">
        <div>
          <div className="app-title">CAD Application Settings</div>
          <div className="app-subtitle">Manage applications, providers and service settings.</div>
        </div>
        <div className="toolbar-actions">
          <div
            className={`app-badge${!application ? ' clickable' : ''}`}
            onClick={() => { if (!application) setView('applications'); }}
          >
            {application
              ? `${application.applicationCode} — ${application.applicationName || application.applicationCode}`
              : 'No application selected'}
          </div>
          <button type="button" className="toolbar-btn" onClick={loadApplications}>
            Refresh
          </button>
        </div>
      </header>
      <main className="main-content">
        {renderContent()}
      </main>
    </div>
  </div>
);
}
export default App;
