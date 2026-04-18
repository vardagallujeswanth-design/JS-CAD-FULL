import { useEffect, useState } from 'react';
import { Routes, Route, Navigate, useNavigate } from 'react-router-dom';

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
  const navigate = useNavigate();

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
    } catch {
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
    } catch {
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

      if (saved?.applicationId) {
        setApplication({
          ...app,
          applicationId: saved.applicationId,
          applicationName: saved.applicationName ?? app.applicationName,
        });
      }
    } catch {
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
    } catch {
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
      const saved = await saveProvider({
        ...prov,
        applicationId: application.applicationId,
      });

      await loadProviders(application.applicationId);
      setEditProvider(null);

      if (saved?.providerId) {
        setProvider(saved);
      }

      navigate('/providers'); 
    } catch {
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

      if (application) {
        await loadProviders(application.applicationId);
      }
    } catch {
      setError('Could not delete provider.');
    } finally {
      setLoading(false);
    }
  };

  // 🔥 FIXED NAVIGATION HERE
  const handleProviderSelect = (prov) => {
    setProvider(prov);
    navigate('/provider-detail');
  };

  const handleEditProvider = (prov) => {
    setEditProvider(prov);
    navigate('/provider-edit');
  };

  const handleAddProvider = () => {
    setEditProvider(null);
    navigate('/provider-add');
  };

  return (
    <div className="app-shell">
      <Sidebar />

      <div className="main-area">
        <header className="app-toolbar">
          <div>
            <div className="app-title">CAD Application Settings</div>
            <div className="app-subtitle">
              Manage applications, providers and service settings
            </div>
          </div>

          <div className="toolbar-actions">
            <div className="app-badge">
              {application
                ? `${application.applicationCode} — ${application.applicationName}`
                : 'No application selected'}
            </div>

            <button
              type="button"
              className="toolbar-btn"
              onClick={loadApplications}
            >
              Refresh
            </button>
          </div>
        </header>

        <main className="main-content">
          {loading && <div className="status-banner">Loading…</div>}
          {error && <div className="status-banner error">{error}</div>}

          <Routes>
            <Route
              path="/"
              element={<Dashboard application={application} provider={provider} />}
            />

            <Route
              path="/applications"
              element={
                <Applications
                  applications={applications}
                  selectedApplication={application}
                  onSelect={setApplication}
                  onSave={handleSaveApplication}
                  onDelete={handleDeleteApplication}
                />
              }
            />

            <Route
              path="/providers"
              element={
                <Providers
                  application={application}
                  providers={providers}
                  selectedProvider={provider}
                  onSelect={handleProviderSelect}
                  onEdit={handleEditProvider}
                  onAdd={handleAddProvider}
                  onDelete={handleDeleteProvider}
                />
              }
            />

            <Route
              path="/provider-detail"
              element={
                <ProviderDetail
                  application={application}
                  provider={provider}
                />
              }
            />

            <Route
              path="/provider-add"
              element={
                <ProviderForm
                  application={application}
                  provider={null}
                  onSave={handleSaveProvider}
                />
              }
            />

            <Route
              path="/provider-edit"
              element={
                <ProviderForm
                  application={application}
                  provider={editProvider}
                  onSave={handleSaveProvider}
                />
              }
            />

            <Route
              path="/retry"
              element={<RetrySettings application={application} provider={provider} />}
            />

            <Route
              path="/email"
              element={<EmailSettings application={application} provider={provider} />}
            />

            <Route
              path="/settings"
              element={<ApplicationSettings application={application} />}
            />

            <Route path="*" element={<Navigate to="/" />} />
          </Routes>
        </main>
      </div>
    </div>
  );
}

export default App;