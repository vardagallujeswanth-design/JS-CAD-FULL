import { useNavigate } from 'react-router-dom';
import './Sidebar.css';

const links = [
  { id: '/applications', label: 'Applications' },
  { id: '/providers', label: 'Providers' },
  { id: '/settings', label: 'App Settings' },
  { id: '/', label: 'Summary' },
];

export default function Sidebar() {
  const navigate = useNavigate();

  return (
    <aside className="sidebar">
      <nav>
        {links.map((link) => (
          <button
            key={link.id}
            onClick={() => navigate(link.id)}
          >
            {link.label}
          </button>
        ))}
      </nav>
    </aside>
  );
}