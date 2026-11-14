import { Link } from 'react-router-dom';
import { buttonVariants } from '@/shared/ui';

export function NotFoundPage() {
  return (
    <div className="mx-auto flex max-w-md flex-1 flex-col items-center justify-center gap-3 p-10 text-center">
      <p className="text-5xl font-bold text-primary">404</p>
      <h1 className="text-xl font-semibold">Page not found</h1>
      <p className="text-sm text-muted">
        The page you are looking for does not exist or was moved.
      </p>
      <Link to="/workspaces" className={buttonVariants({ variant: 'primary' })}>
        Back to workspaces
      </Link>
    </div>
  );
}
