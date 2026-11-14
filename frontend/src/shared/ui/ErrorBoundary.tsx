import { Component, type ErrorInfo, type ReactNode } from 'react';
import { Button } from './Button';

interface ErrorBoundaryProps {
  children: ReactNode;
  /** Custom fallback. Receives the error and a reset function. */
  fallback?: (error: Error, reset: () => void) => ReactNode;
  onError?: (error: Error, info: ErrorInfo) => void;
}

type ErrorBoundaryState = { status: 'ok' } | { status: 'failed'; error: Error };

export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  override state: ErrorBoundaryState = { status: 'ok' };

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { status: 'failed', error };
  }

  override componentDidCatch(error: Error, info: ErrorInfo): void {
    this.props.onError?.(error, info);
  }

  private reset = () => this.setState({ status: 'ok' });

  override render(): ReactNode {
    if (this.state.status === 'ok') {
      return this.props.children;
    }

    const { error } = this.state;
    if (this.props.fallback) {
      return this.props.fallback(error, this.reset);
    }

    return (
      <div role="alert" className="mx-auto max-w-md space-y-3 p-8 text-center">
        <h2 className="text-lg font-semibold">Something went wrong</h2>
        <p className="text-sm text-muted">{error.message}</p>
        <Button onClick={this.reset}>Try again</Button>
      </div>
    );
  }
}
