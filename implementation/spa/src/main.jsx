import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { Amplify } from 'aws-amplify';
import App from './App';
import { getAmplifyConfig } from './config/amplifyConfig';
import './index.css';

Amplify.configure(getAmplifyConfig());

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
