import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Link, useNavigate } from 'react-router-dom';
import { applyServerFieldErrors } from '@/shared/lib/form-errors';
import { Button, TextField } from '@/shared/ui';
import { AuthLayout } from '../components/AuthLayout';
import { useRegister } from '../hooks/useRegister';
import { registerSchema, type RegisterValues } from '../schemas';

export function RegisterPage() {
  const navigate = useNavigate();
  const registerUser = useRegister();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<RegisterValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: { displayName: '', email: '', password: '', confirmPassword: '' },
  });

  const onSubmit = handleSubmit(({ displayName, email, password }) =>
    registerUser.mutate(
      { displayName, email, password },
      {
        onSuccess: () => navigate('/workspaces', { replace: true }),
        onError: (error) => {
          if (error.code === 'auth.email_taken') {
            setError('email', {
              type: 'server',
              message: 'An account with this email already exists.',
            });
            return;
          }

          const handled = applyServerFieldErrors(
            error,
            ['email', 'displayName', 'password'],
            setError,
          );
          if (!handled) {
            setError('root', { message: error.message });
          }
        },
      },
    ),
  );

  return (
    <AuthLayout
      title="Create your account"
      subtitle="Start organizing work with your team"
      footer={
        <>
          Already have an account?{' '}
          <Link to="/login" className="font-medium text-primary hover:underline">
            Sign in
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
          label="Name"
          autoComplete="name"
          error={errors.displayName?.message}
          {...register('displayName')}
        />
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
          autoComplete="new-password"
          hint="At least 8 characters, with a digit and a lowercase letter."
          error={errors.password?.message}
          {...register('password')}
        />
        <TextField
          label="Confirm password"
          type="password"
          autoComplete="new-password"
          error={errors.confirmPassword?.message}
          {...register('confirmPassword')}
        />
        <Button type="submit" className="w-full" loading={registerUser.isPending}>
          Create account
        </Button>
      </form>
    </AuthLayout>
  );
}
