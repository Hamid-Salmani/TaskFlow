import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { toast, useToastStore } from './toast-store';
import { Toaster } from './Toaster';

describe('Toaster', () => {
  afterEach(() => {
    act(() => useToastStore.getState().clear());
    vi.useRealTimers();
  });

  it('renders a polite live region even when there are no toasts', () => {
    render(<Toaster />);

    const region = screen.getByRole('region', { name: 'Notifications' });
    expect(region).toHaveAttribute('aria-live', 'polite');
  });

  it('announces success toasts as status and errors as alerts', () => {
    render(<Toaster />);

    act(() => {
      toast.success('Saved', 'Card updated');
      toast.error('Move failed');
    });

    expect(screen.getByRole('status')).toHaveTextContent('Saved');
    expect(screen.getByRole('status')).toHaveTextContent('Card updated');
    expect(screen.getByRole('alert')).toHaveTextContent('Move failed');
  });

  it('dismisses a toast with its close button', async () => {
    const user = userEvent.setup();
    render(<Toaster />);
    act(() => {
      toast.info('Heads up');
    });

    await user.click(screen.getByRole('button', { name: 'Dismiss notification' }));

    expect(screen.queryByText('Heads up')).not.toBeInTheDocument();
  });

  it('removes toasts automatically after their duration', () => {
    vi.useFakeTimers();
    render(<Toaster />);
    act(() => {
      useToastStore.getState().push({ kind: 'info', title: 'Temporary', durationMs: 1000 });
    });
    expect(screen.getByText('Temporary')).toBeInTheDocument();

    act(() => {
      vi.advanceTimersByTime(1001);
    });

    expect(screen.queryByText('Temporary')).not.toBeInTheDocument();
  });

  it('keeps at most four toasts visible', () => {
    render(<Toaster />);

    act(() => {
      for (let i = 1; i <= 6; i += 1) {
        toast.info(`Toast ${i}`);
      }
    });

    expect(screen.getAllByRole('status')).toHaveLength(4);
    expect(screen.queryByText('Toast 1')).not.toBeInTheDocument();
    expect(screen.getByText('Toast 6')).toBeInTheDocument();
  });
});
