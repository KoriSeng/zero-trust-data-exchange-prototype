import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

export default function Header() {
  const navigate = useNavigate();
  const { profile, user, signOut } = useAuth();
  const [error, setError] = useState('');
  const [showUserMenu, setShowUserMenu] = useState(false);

  const displayName = profile?.displayName ?? user?.username ?? 'Authenticated User';
  const organizationName = profile?.organizationName ?? 'Unknown organisation';

  async function handleSignOut() {
    setError('');
    try {
      await signOut();
      navigate('/login', { replace: true });
    } catch (signOutError) {
      setError(signOutError.message ?? 'Could not sign out.');
    }
  }

  return (
    <header className="modern-header">
      <div>
        <div className="header-brand">
          <div className="brand-icon">🧬</div>
          <div className="brand-text">
            <h1 className="brand-title">Zero Trust Data Exchange</h1>
            <p className="brand-subtitle">Bio Research Data Sharing Platform</p>
          </div>
        </div>

        <div className="header-right">
          <button type="button" className="icon-button" aria-label="Notifications" title="Notifications">
          <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
            <path
              d="M15 6.66667C15 5.34058 14.4732 4.06881 13.5355 3.13113C12.5979 2.19345 11.3261 1.66667 10 1.66667C8.67392 1.66667 7.40215 2.19345 6.46447 3.13113C5.52678 4.06881 5 5.34058 5 6.66667C5 12.5 2.5 14.1667 2.5 14.1667H17.5C17.5 14.1667 15 12.5 15 6.66667Z"
              stroke="currentColor"
              strokeWidth="1.5"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
            <path
              d="M11.4417 17.5C11.2952 17.7526 11.0849 17.9622 10.8319 18.1079C10.5788 18.2537 10.292 18.3304 10 18.3304C9.70802 18.3304 9.42118 18.2537 9.16816 18.1079C8.91513 17.9622 8.70484 17.7526 8.55835 17.5"
              stroke="currentColor"
              strokeWidth="1.5"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
        </button>

        <button type="button" className="icon-button" aria-label="Settings" title="Settings">
          <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
            <path
              d="M10 12.5C11.3807 12.5 12.5 11.3807 12.5 10C12.5 8.61929 11.3807 7.5 10 7.5C8.61929 7.5 7.5 8.61929 7.5 10C7.5 11.3807 8.61929 12.5 10 12.5Z"
              stroke="currentColor"
              strokeWidth="1.5"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
            <path
              d="M16.1667 12.5C16.0557 12.7513 16.0226 13.0301 16.0717 13.3006C16.1209 13.5711 16.2501 13.8203 16.4417 14.0167L16.4917 14.0667C16.6466 14.2215 16.7694 14.4054 16.8534 14.6079C16.9373 14.8104 16.9808 15.0276 16.9808 15.2469C16.9808 15.4663 16.9373 15.6835 16.8534 15.886C16.7694 16.0885 16.6466 16.2724 16.4917 16.4273C16.3368 16.5822 16.1529 16.705 15.9504 16.7889C15.7479 16.8729 15.5307 16.9164 15.3113 16.9164C15.092 16.9164 14.8748 16.8729 14.6723 16.7889C14.4698 16.705 14.2859 16.5822 14.131 16.4273L14.081 16.3773C13.8846 16.1857 13.6354 16.0565 13.3649 16.0073C13.0944 15.9582 12.8156 15.9913 12.5643 16.1023C12.3175 16.2082 12.1068 16.3842 11.9579 16.6091C11.809 16.8341 11.7284 17.0982 11.7257 17.3689V17.5C11.7257 17.942 11.5501 18.366 11.2375 18.6786C10.9249 18.9911 10.5009 19.1667 10.059 19.1667C9.61707 19.1667 9.19301 18.9911 8.88044 18.6786C8.56788 18.366 8.39232 17.942 8.39232 17.5V17.425C8.38418 17.1461 8.29432 16.8753 8.13378 16.6465C7.97325 16.4178 7.74932 16.2409 7.49065 16.1373C7.23935 16.0263 6.96055 15.9931 6.69006 16.042C6.41956 16.0908 6.17034 16.22 5.97399 16.4117L5.92399 16.4617C5.76911 16.6166 5.5852 16.7394 5.38271 16.8233C5.18021 16.9072 4.96301 16.9508 4.74365 16.9508C4.5243 16.9508 4.3071 16.9072 4.10461 16.8233C3.90211 16.7394 3.71821 16.6166 3.56332 16.4617C3.40845 16.3068 3.28563 16.1229 3.20169 15.9204C3.11775 15.7179 3.0742 15.5007 3.0742 15.2813C3.0742 15.062 3.11775 14.8448 3.20169 14.6423C3.28563 14.4398 3.40845 14.2559 3.56332 14.101L3.61332 14.051C3.80499 13.8546 3.93417 13.6054 3.98332 13.3349C4.03247 13.0644 3.99932 12.7856 3.88832 12.5343C3.78239 12.2876 3.60647 12.0768 3.38149 11.9279C3.15652 11.779 2.8924 11.6984 2.62165 11.6957H2.49999C2.05806 11.6957 1.634 11.5201 1.32143 11.2075C1.00887 10.8949 0.833313 10.4709 0.833313 10.029C0.833313 9.58703 1.00887 9.16297 1.32143 8.85041C1.634 8.53784 2.05806 8.36229 2.49999 8.36229H2.57499C2.85393 8.35414 3.12469 8.26428 3.35345 8.10375C3.58221 7.94321 3.75914 7.71929 3.86265 7.46062C3.97365 7.20932 4.00681 6.93052 3.95766 6.66003C3.9085 6.38953 3.77932 6.14031 3.58765 5.94396L3.53765 5.89396C3.38279 5.73907 3.25996 5.55516 3.17603 5.35267C3.09209 5.15017 3.04854 4.93297 3.04854 4.71362C3.04854 4.49427 3.09209 4.27706 3.17603 4.07457C3.25996 3.87207 3.38279 3.68817 3.53765 3.53329C3.69254 3.37842 3.87644 3.25559 4.07894 3.17166C4.28144 3.08772 4.49864 3.04417 4.71799 3.04417C4.93734 3.04417 5.15454 3.08772 5.35704 3.17166C5.55953 3.25559 5.74344 3.37842 5.89832 3.53329L5.94832 3.58329C6.14467 3.77495 6.39389 3.90414 6.66439 3.95329C6.93488 4.00243 7.21368 3.96928 7.46499 3.85829H7.54999C7.79666 3.75235 8.00747 3.57643 8.15634 3.35146C8.3052 3.12649 8.38578 2.86237 8.38832 2.59162V2.47C8.38832 2.02807 8.56388 1.60401 8.87644 1.29145C9.18901 0.978883 9.61307 0.803329 10.055 0.803329C10.4969 0.803329 10.921 0.978883 11.2336 1.29145C11.5461 1.60401 11.7217 2.02807 11.7217 2.47V2.545C11.7242 2.81575 11.8048 3.07987 11.9537 3.30484C12.1025 3.52981 12.3133 3.70573 12.56 3.81167C12.8113 3.92267 13.0901 3.95581 13.3606 3.90666C13.6311 3.85751 13.8803 3.72833 14.0767 3.53667L14.1267 3.48667C14.2815 3.3318 14.4654 3.20897 14.6679 3.12503C14.8704 3.0411 15.0876 2.99755 15.307 2.99755C15.5263 2.99755 15.7435 3.0411 15.946 3.12503C16.1485 3.20897 16.3324 3.3318 16.4873 3.48667C16.6422 3.64155 16.765 3.82546 16.8489 4.02795C16.9329 4.23045 16.9764 4.44765 16.9764 4.667C16.9764 4.88635 16.9329 5.10355 16.8489 5.30605C16.765 5.50854 16.6422 5.69245 16.4873 5.84733L16.4373 5.89733C16.2457 6.09368 16.1165 6.3429 16.0673 6.6134C16.0182 6.88389 16.0513 7.16269 16.1623 7.414V7.5C16.2683 7.74667 16.4442 7.95747 16.6692 8.10634C16.8941 8.25521 17.1583 8.33578 17.429 8.33833H17.55C17.9919 8.33833 18.416 8.51389 18.7286 8.82645C19.0411 9.13902 19.2167 9.56307 19.2167 10.005C19.2167 10.4469 19.0411 10.871 18.7286 11.1836C18.416 11.4961 17.9919 11.6717 17.55 11.6717H17.475C17.2042 11.6742 16.9401 11.7548 16.7151 11.9037C16.4902 12.0525 16.3142 12.2633 16.2083 12.51V12.5Z"
              stroke="currentColor"
              strokeWidth="1.5"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
        </button>

        <div className="user-menu-wrapper">
          <button
            type="button"
            className="user-menu-trigger"
            onClick={() => setShowUserMenu(!showUserMenu)}
            aria-expanded={showUserMenu}
            aria-haspopup="true"
          >
            <div className="user-avatar">
              {displayName.charAt(0).toUpperCase()}
            </div>
            <div className="user-info">
              <span className="user-name">{displayName}</span>
              <span className="user-org">{organizationName}</span>
            </div>
            <svg width="16" height="16" viewBox="0 0 16 16" fill="none" className="dropdown-icon">
              <path
                d="M4 6L8 10L12 6"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          </button>

          {showUserMenu && (
            <div className="user-dropdown">
              <div className="dropdown-section">
                <div className="dropdown-user-details">
                  <strong>{displayName}</strong>
                  <span>{organizationName}</span>
                  <span className="user-roles">{profile?.roles?.join(', ') ?? 'No roles'}</span>
                </div>
              </div>
              <div className="dropdown-divider" />
              <button type="button" className="dropdown-item" onClick={handleSignOut}>
                <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
                  <path
                    d="M6 14H3.33333C2.97971 14 2.64057 13.8595 2.39052 13.6095C2.14048 13.3594 2 13.0203 2 12.6667V3.33333C2 2.97971 2.14048 2.64057 2.39052 2.39052C2.64057 2.14048 2.97971 2 3.33333 2H6"
                    stroke="currentColor"
                    strokeWidth="1.5"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  />
                  <path
                    d="M10.6667 11.3333L14 8L10.6667 4.66666"
                    stroke="currentColor"
                    strokeWidth="1.5"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  />
                  <path
                    d="M14 8H6"
                    stroke="currentColor"
                    strokeWidth="1.5"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  />
                </svg>
                Sign out
              </button>
            </div>
          )}
        </div>
      </div>
    </div>

    {error && <p className="error-banner">{error}</p>}
  </header>
);
}
