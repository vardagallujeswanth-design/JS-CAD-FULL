import './Sidebar.css';

const links = [
  { id: 'applications', label: 'Applications' },
  { id: 'providers', label: 'Providers' },
  { id: 'settings', label: 'App Settings' },
  { id: 'dashboard', label: 'Summary' },

];

export default function Sidebar({ active, onNavigate }) {
  return (
    <aside className="sidebar">
      <div className="sidebar-brand">
        <strong>  CAD Admin</strong>
      </div>
      <nav>
        {links.map((link) => (
          <button
            key={link.id}
            type="button"
            className={link.id === active ? 'active' : ''}
            onClick={() => onNavigate(link.id)}
          >
            {link.label}
          </button>
        ))}
      </nav>
    </aside>
  );
}
