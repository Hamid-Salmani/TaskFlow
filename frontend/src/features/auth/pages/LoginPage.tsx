import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { Button, TextField } from '@/shared/ui';
import { AuthLayout } from '../components/AuthLayout';
import { readRedirectTarget } from '../components/ProtectedRoute';
import { useLogin } from '../hooks/useLogin';
import { loginSchema, type LoginValues } from '../schemas';

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const login = useLogin();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  });

  const onSubmit = handleSubmit((values) =>
    login.mutate(values, {
      onSuccess: () =>
        navigate(readRedirectTarget(location.state) ?? '/workspaces', { replace: true }),
      onError: (error) =>
        setError('root', {
          message:
            error.code === 'auth.invalid_credentials'
              ? 'Invalid email or password.'
              : error.message,
        }),
    }),
  );

  return (
    <AuthLayout
      title="Welcome back"
      subtitle="Sign in to continue to TaskFlow"
      footer={
        <>
          New here?{' '}
          <Link to="/register" className="font-medium text-primary hover:underline">
            Create an account
          </Link>
        </>
      }
    >
      <form onSubmit={onSubmit} noValidate className="space-y-4">
        {errors.root ? (
          <p
            role="alert"
            className="rounded-md bg-danger/10 px-3 py-2 text-sm font-medium text-danger"
          >
            {errors.root.message}
          </p>
        ) : null}
        <TextField
          label="Email"
          type="email"
          autoComplete="email"
          error={errors.email?.message}
          {...register('email')}
        />
        <TextField
          label="Password"
          type="password"
          autoComplete="current-password"
          error={errors.password?.message}
          {...register('password')}
        />
        <Button type="submit" className="w-full" loading={login.isPending}>
          Sign in
        </Button>
      </form>
    </AuthLayout>
  );
}
