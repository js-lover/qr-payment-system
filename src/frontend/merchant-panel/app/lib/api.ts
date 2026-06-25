// app/lib/api.ts
//
// Merchant Panel API istemcisi.
// Axios instance — cookie tabanlı veya Authorization header ile JWT token eklenir.
// Tüm istekler Next.js proxy üzerinden geçer (/api/proxy).

import axios from 'axios';

export const apiClient = axios.create({
  baseURL: '/api',
  timeout: 15000,
  headers: { 'Content-Type': 'application/json' },
});

// Token yönetimi: localStorage (CSR) veya server-side fetch (SSR)
export function setAuthToken(token: string) {
  apiClient.defaults.headers.common['Authorization'] = `Bearer ${token}`;
  if (typeof window !== 'undefined') {
    localStorage.setItem('merchantToken', token);
  }
}

export function clearAuthToken() {
  delete apiClient.defaults.headers.common['Authorization'];
  if (typeof window !== 'undefined') {
    localStorage.removeItem('merchantToken');
  }
}

export function loadStoredToken() {
  if (typeof window === 'undefined') return;
  const token = localStorage.getItem('merchantToken');
  if (token) {
    apiClient.defaults.headers.common['Authorization'] = `Bearer ${token}`;
  }
}

// API yanıt tipi — tüm backend endpoint'lerinde ortak format
export interface ApiResponse<T> {
  success: boolean;
  data: T;
  error?: { code: string; message: string };
}

// İşlem listesi için tipler
export interface Transaction {
  id: string;
  status: string;
  amount: number;
  currency: string;
  merchantTitle: string;
  terminalId: string;
  isoResponseCode?: string;
  occurredAt: string;
  completedAt?: string;
}

export interface TransactionSummary {
  totalCount: number;
  totalAmount: number;
  successCount: number;
  failedCount: number;
  currency: string;
}
