import { render, screen } from '@testing-library/react';
import { ConnectionIndicator } from './ConnectionIndicator';

describe('ConnectionIndicator', () => {
  it.each(['connecting', 'connected'] as const)('shows nothing while %s', (status) => {
    render(<ConnectionIndicator status={status} />);

    expect(screen.getByRole('status')).toBeEmptyDOMElement();
  });

  it('announces reconnecting in a polite live region', () => {
    render(<ConnectionIndicator status="reconnecting" />);

    expect(screen.getByRole('status')).toHaveTextContent('Reconnecting');
    expect(screen.getByRole('status')).toHaveAttribute('aria-live', 'polite');
  });

  it('explains when the connection is lost', () => {
    render(<ConnectionIndicator status="disconnected" />);

    expect(screen.getByRole('status')).toHaveTextContent(/offline/i);
  });
});
