import { BrowserRouter } from 'react-router-dom';
import { QueryProvider } from '@/providers/QueryProvider';
import { AppRoutes } from '@/routes/AppRoutes';
import './App.css';

function App() {
  return (
    <QueryProvider>
      <BrowserRouter>
        <AppRoutes />
      </BrowserRouter>
    </QueryProvider>
  );
}

export default App;
