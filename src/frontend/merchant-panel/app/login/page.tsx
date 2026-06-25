// app/login/page.tsx
//
// Merchant Panel giriş sayfası.
// Kullanıcı adı + şifre → POST /auth/token (AuthService)
// JWT token alınır, HTTP-only cookie'ye yazılır.
// Başarılı girişte dashboard'a yönlendirilir.

import LoginClient from '../components/LoginClient';

export default function LoginPage() {
  return <LoginClient />;
}
