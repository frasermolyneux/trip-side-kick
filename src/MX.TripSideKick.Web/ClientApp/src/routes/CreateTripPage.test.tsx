import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { AuthContext } from '../auth/AuthContext';
import { server } from '../mocks/server';
import { CreateTripPage } from './CreateTripPage';

const navigateSpy = vi.hoisted(() => vi.fn());

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
  return { ...actual, useNavigate: () => navigateSpy };
});

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <AuthContext.Provider
        value={{
          auth: { isAuthenticated: true, displayName: 'Test User', subjectId: 'subject-me' },
          config: {
            applicationInsightsConnectionString: null,
            signInEnabled: true,
            loginUrl: '/v1/auth/login',
            logoutUrl: '/v1/auth/logout'
          }
        }}
      >
        <MemoryRouter initialEntries={['/trips/new']}>
          <Routes>
            <Route path="/trips/new" element={<CreateTripPage />} />
          </Routes>
        </MemoryRouter>
      </AuthContext.Provider>
    </QueryClientProvider>
  );
}

describe('CreateTripPage', () => {
  it('requires only a trip name and navigates to the new trip on success', async () => {
    server.use(
      http.post('/v1/trips', async ({ request }) => {
        const body = (await request.json()) as { name: string };
        return HttpResponse.json({ id: 'new-trip', name: body.name, destinations: [] }, { status: 201 });
      })
    );

    renderPage();

    await userEvent.type(screen.getByTestId('trip-name-input'), 'A Quick Getaway');
    await userEvent.click(screen.getByTestId('submit-create-trip'));

    await waitFor(() => expect(navigateSpy).toHaveBeenCalledWith('/trips/new-trip'));
  });

  it('shows a validation error when the name is left blank', async () => {
    renderPage();

    await userEvent.click(screen.getByTestId('submit-create-trip'));

    expect(await screen.findByText(/give the trip a name/i)).toBeInTheDocument();
  });

  it('shows the start/end date fields only once dates are not undecided', async () => {
    renderPage();

    expect(screen.queryByTestId('trip-start-date-input')).not.toBeInTheDocument();

    const dateStatusSelect = screen.getByTestId('trip-date-status-select');
    await userEvent.click(within(dateStatusSelect).getByRole('combobox'));
    await userEvent.click(await screen.findByText(/confirmed - exact dates/i));

    expect(await screen.findByTestId('trip-start-date-input')).toBeInTheDocument();
  });

  it('shows a submit error when trip creation fails', async () => {
    server.use(http.post('/v1/trips', () => HttpResponse.json({ title: 'Boom' }, { status: 500 })));

    renderPage();

    await userEvent.type(screen.getByTestId('trip-name-input'), 'Will Fail');
    await userEvent.click(screen.getByTestId('submit-create-trip'));

    expect(await screen.findByText(/we could not create the trip/i)).toBeInTheDocument();
  });
});
