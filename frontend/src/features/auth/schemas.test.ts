import { loginSchema, registerSchema } from './schemas';

describe('loginSchema', () => {
  it('accepts a valid email and a password', () => {
    expect(loginSchema.safeParse({ email: 'ada@test.dev', password: 'x' }).success).toBe(true);
  });

  it('trims the email before validating', () => {
    const result = loginSchema.safeParse({ email: '  ada@test.dev  ', password: 'x' });

    expect(result.success && result.data.email).toBe('ada@test.dev');
  });

  it.each([
    ['', 'Email is required'],
    ['not-an-email', 'Enter a valid email address'],
  ])('rejects the email %j', (email, message) => {
    const result = loginSchema.safeParse({ email, password: 'x' });

    expect(result.success).toBe(false);
    expect(result.error?.issues[0]?.message).toBe(message);
  });

  it('requires a password', () => {
    const result = loginSchema.safeParse({ email: 'ada@test.dev', password: '' });

    expect(result.error?.issues[0]?.message).toBe('Password is required');
  });
});

describe('registerSchema', () => {
  const valid = {
    displayName: 'Ada',
    email: 'ada@test.dev',
    password: 'Password123',
    confirmPassword: 'Password123',
  };

  it('accepts valid input', () => {
    expect(registerSchema.safeParse(valid).success).toBe(true);
  });

  it.each([
    ['short1', 'Password must be at least 8 characters'],
    ['NoDigitsHere', 'Password must contain a digit'],
    ['12345678', 'Password must contain a lowercase letter'],
  ])('rejects the weak password %j', (password, message) => {
    const result = registerSchema.safeParse({ ...valid, password, confirmPassword: password });

    expect(result.error?.issues.map((issue) => issue.message)).toContain(message);
  });

  it('reports mismatching confirmation on the confirmPassword field', () => {
    const result = registerSchema.safeParse({ ...valid, confirmPassword: 'Different123' });

    const issue = result.error?.issues[0];
    expect(issue?.path).toEqual(['confirmPassword']);
    expect(issue?.message).toBe('Passwords do not match');
  });

  it('requires a name and limits its length', () => {
    expect(registerSchema.safeParse({ ...valid, displayName: '   ' }).success).toBe(false);
    expect(registerSchema.safeParse({ ...valid, displayName: 'x'.repeat(101) }).success).toBe(
      false,
    );
  });
});
