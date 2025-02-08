import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { App } from './App';
import { CartProvider } from './cart';
import { DeliveryLocationProvider } from './location';
import './styles.css';

const root = document.getElementById('root');
if (!root) throw new Error('Missing #root element');

createRoot(root).render(
  <StrictMode>
    <BrowserRouter>
      <DeliveryLocationProvider>
        <CartProvider>
          <App />
        </CartProvider>
      </DeliveryLocationProvider>
    </BrowserRouter>
  </StrictMode>,
);
