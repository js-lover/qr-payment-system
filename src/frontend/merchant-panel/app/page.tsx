// app/page.tsx
//
// Merchant Panel ana sayfası — Dashboard.
// Server Component: oturum kontrolü yapılır, giriş yoksa /login'e yönlendirilir.
// İstemci tarafı komponenti DashboardClient, günlük özet ve son işlemleri gösterir.

import { redirect } from 'next/navigation';
import { cookies } from 'next/headers';
import DashboardClient from './components/DashboardClient';

export default async function DashboardPage() {
  const cookieStore = await cookies();
  const token = cookieStore.get('merchantToken')?.value;

  // Token yoksa login sayfasına yönlendir
  if (!token) {
    redirect('/login');
  }

  return <DashboardClient token={token} />;
}
