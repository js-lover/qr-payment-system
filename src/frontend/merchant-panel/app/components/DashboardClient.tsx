// app/components/DashboardClient.tsx
//
// Merchant Panel dashboard (Client Component).
// JWT token ile ReportingService'e istek atar.
// Gösterilen veriler:
//   - Günlük işlem özeti (toplam tutar, adet, başarı/başarısız)
//   - Son 10 işlem listesi (tablo)
//
// Sayfa yenilendiğinde veri yeniden çekilir.

'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import axios from 'axios';
import { Transaction, TransactionSummary } from '../lib/api';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? 'http://localhost:8000';

interface Props {
  token: string;
}

const STATUS_LABELS: Record<string, string> = {
  COMPLETED: 'Tamamlandı',
  FAILED: 'Başarısız',
  REVERSED: 'İptal Edildi',
  PENDING: 'Bekliyor',
};

const STATUS_COLORS: Record<string, string> = {
  COMPLETED: 'bg-green-100 text-green-800',
  FAILED: 'bg-red-100 text-red-800',
  REVERSED: 'bg-yellow-100 text-yellow-800',
  PENDING: 'bg-blue-100 text-blue-800',
};

export default function DashboardClient({ token }: Props) {
  const router = useRouter();
  const [summary, setSummary] = useState<TransactionSummary | null>(null);
  const [transactions, setTransactions] = useState<Transaction[]>([]);
  const [loading, setLoading] = useState(true);

  const headers = { Authorization: `Bearer ${token}` };

  useEffect(() => {
    fetchData();
  }, []);

  const fetchData = async () => {
    try {
      const today = new Date().toISOString().split('T')[0];
      const [summaryResp, txResp] = await Promise.all([
        axios.get(`${API_BASE}/reports/summary?from=${today}&to=${today}`, { headers }),
        axios.get(`${API_BASE}/reports/transactions?page=1&pageSize=10`, { headers }),
      ]);
      setSummary(summaryResp.data.data);
      setTransactions(txResp.data.data.items ?? []);
    } catch {
      // 401 → oturum süresi dolmuş
      document.cookie = 'merchantToken=; path=/; max-age=0';
      router.push('/login');
    } finally {
      setLoading(false);
    }
  };

  const handleLogout = () => {
    document.cookie = 'merchantToken=; path=/; max-age=0';
    router.push('/login');
  };

  const formatAmount = (amount: number, currency = 'TRY') =>
    new Intl.NumberFormat('tr-TR', { style: 'currency', currency }).format(amount);

  const formatDate = (iso: string) =>
    new Intl.DateTimeFormat('tr-TR', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    }).format(new Date(iso));

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header */}
      <header className="bg-white border-b border-gray-200 px-6 py-4 flex items-center justify-between">
        <h1 className="text-xl font-bold text-gray-900">QR Ödeme — Merchant Panel</h1>
        <button
          onClick={handleLogout}
          className="text-sm text-gray-500 hover:text-gray-700 transition-colors"
        >
          Çıkış Yap
        </button>
      </header>

      <main className="max-w-7xl mx-auto px-6 py-8">
        {loading ? (
          <div className="flex items-center justify-center h-64">
            <div className="w-8 h-8 border-4 border-indigo-600 border-t-transparent rounded-full animate-spin" />
          </div>
        ) : (
          <>
            {/* Özet Kartlar */}
            <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
              <SummaryCard
                label="Toplam İşlem"
                value={summary?.totalCount?.toString() ?? '0'}
              />
              <SummaryCard
                label="Toplam Ciro"
                value={formatAmount(summary?.totalAmount ?? 0)}
              />
              <SummaryCard
                label="Başarılı"
                value={summary?.successCount?.toString() ?? '0'}
                color="text-green-600"
              />
              <SummaryCard
                label="Başarısız"
                value={summary?.failedCount?.toString() ?? '0'}
                color="text-red-600"
              />
            </div>

            {/* Son İşlemler */}
            <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
              <div className="px-6 py-4 border-b border-gray-100">
                <h2 className="text-base font-semibold text-gray-900">Son İşlemler</h2>
              </div>

              {transactions.length === 0 ? (
                <p className="text-center text-gray-400 py-12">Henüz işlem bulunmuyor.</p>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="bg-gray-50 text-left">
                        {['İşlem ID', 'Tarih', 'Terminal', 'Tutar', 'Durum'].map((h) => (
                          <th key={h} className="px-4 py-3 font-medium text-gray-500">
                            {h}
                          </th>
                        ))}
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-50">
                      {transactions.map((tx) => (
                        <tr key={tx.id} className="hover:bg-gray-50 transition-colors">
                          <td className="px-4 py-3 font-mono text-xs text-gray-500">
                            {tx.id.slice(0, 8)}...
                          </td>
                          <td className="px-4 py-3 text-gray-700">{formatDate(tx.occurredAt)}</td>
                          <td className="px-4 py-3 text-gray-700">{tx.terminalId}</td>
                          <td className="px-4 py-3 font-semibold text-gray-900">
                            {formatAmount(tx.amount, tx.currency)}
                          </td>
                          <td className="px-4 py-3">
                            <span
                              className={`inline-flex items-center px-2 py-1 rounded-full text-xs font-medium ${
                                STATUS_COLORS[tx.status] ?? 'bg-gray-100 text-gray-700'
                              }`}
                            >
                              {STATUS_LABELS[tx.status] ?? tx.status}
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </>
        )}
      </main>
    </div>
  );
}

function SummaryCard({
  label,
  value,
  color = 'text-gray-900',
}: {
  label: string;
  value: string;
  color?: string;
}) {
  return (
    <div className="bg-white rounded-2xl border border-gray-100 shadow-sm px-6 py-5">
      <p className="text-sm text-gray-500 mb-1">{label}</p>
      <p className={`text-2xl font-bold ${color}`}>{value}</p>
    </div>
  );
}
