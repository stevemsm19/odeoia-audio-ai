export type User = { name: string; email: string };

type LoginCredentials = { email: string; password?: string };
type SignupValues = { name?: string; email: string; password?: string };

const STORAGE_KEY = "odeoia-demo-user";
const wait = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms));

export const currentUser = (): User | null => {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as User) : null;
  } catch {
    return null;
  }
};

export async function login({ email }: LoginCredentials): Promise<User> {
  await wait(700);
  const user: User = { name: email.split("@")[0], email };
  localStorage.setItem(STORAGE_KEY, JSON.stringify(user));
  return user;
}

export async function signup({ name, email }: SignupValues): Promise<User> {
  await wait(800);
  const user: User = { name: name?.trim() || email.split("@")[0], email };
  localStorage.setItem(STORAGE_KEY, JSON.stringify(user));
  return user;
}

export const logout = (): void => localStorage.removeItem(STORAGE_KEY);
