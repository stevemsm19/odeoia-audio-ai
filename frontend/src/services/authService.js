const STORAGE_KEY = "odeoia-demo-user";
const wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
export const currentUser = () => {
  try {
    return JSON.parse(localStorage.getItem(STORAGE_KEY));
  } catch {
    return null;
  }
};
export async function login({ email }) {
  await wait(700);
  const user = { name: email.split("@")[0], email };
  localStorage.setItem(STORAGE_KEY, JSON.stringify(user));
  return user;
}
export async function signup({ name, email }) {
  await wait(800);
  const user = { name: name.trim() || email.split("@")[0], email };
  localStorage.setItem(STORAGE_KEY, JSON.stringify(user));
  return user;
}
export const logout = () => localStorage.removeItem(STORAGE_KEY);
