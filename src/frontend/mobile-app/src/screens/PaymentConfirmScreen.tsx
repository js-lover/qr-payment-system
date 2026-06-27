// src/frontend/mobile-app / src/screens/PaymentConfirmScreen.tsx
//
// Ödeme onay ekranı.
// QR tarandıktan sonra işyeri adı ve tutar gösterilir.
// Kullanıcı "Öde" butonuna basınca:
//   1. POST /payments/confirm → TransactionService
//   2. SignalR WebSocket bağlantısı kurulur (ödeme sonucu beklenir)
//   3. "PaymentResult" eventi geldiğinde sonuç gösterilir

import React, { useState, useEffect, useRef } from 'react';
import {
  View, Text, TouchableOpacity, StyleSheet,
  ActivityIndicator, Alert
} from 'react-native';
import { apiClient } from '../api/client';

interface QrInfo {
  merchantTitle: string;
  amount: number;
  currency: string;
  remainingSeconds: number;
}

interface Props {
  qrToken: string;
  onComplete: (success: boolean) => void;
}

const API_BASE_URL = process.env.EXPO_PUBLIC_API_BASE_URL ?? 'http://localhost:8000';

export function PaymentConfirmScreen({ qrToken, onComplete }: Props) {
  const [qrInfo, setQrInfo] = useState<QrInfo | null>(null);
  const [loading, setLoading] = useState(true);
  const [paying, setPaying] = useState(false);
  const [countdown, setCountdown] = useState(90);
  const payingRef = useRef(false);
  const connectionRef = useRef<null>(null);

  useEffect(() => {
    fetchQrInfo();
    return () => {
      connectionRef.current?.stop();
    };
  }, [qrToken]);

  useEffect(() => {
    if (countdown <= 0) {
      Alert.alert('Süre Doldu', 'QR kodun süresi doldu.');
      onComplete(false);
      return;
    }
    const timer = setTimeout(() => setCountdown((c) => c - 1), 1000);
    return () => clearTimeout(timer);
  }, [countdown]);

  const fetchQrInfo = async () => {
    try {
      const response = await apiClient.get(`/qr/${qrToken}/validate`);
      const { data } = response.data;
      setQrInfo({
        merchantTitle: data.merchantTitle,
        amount: data.amount,
        currency: data.currency ?? 'TRY',
        remainingSeconds: data.remainingSeconds,
      });
      setCountdown(data.remainingSeconds);
    } catch {
      Alert.alert('Hata', 'QR kodu okunamadı.');
      onComplete(false);
    } finally {
      setLoading(false);
    }
  };

  const handlePay = async () => {
    payingRef.current = true;
    setPaying(true);
    try {
      // POST /payments/confirm tüm akışı senkron işler (provision → bank → kafka → signalR POS).
      // HTTP yanıtında zaten nihai status var — ayrıca SignalR beklemeye gerek yok.
      const response = await apiClient.post('/payments/confirm', { qrToken });
      const { data } = response.data;

      payingRef.current = false;
      setPaying(false);

      if (data.status === 'COMPLETED') {
        Alert.alert('Ödeme Başarılı', 'İşleminiz tamamlandı.');
        onComplete(true);
      } else {
        Alert.alert('Ödeme Başarısız', 'İşlem gerçekleştirilemedi.');
        onComplete(false);
      }
    } catch (error: any) {
      payingRef.current = false;
      setPaying(false);
      const msg = error.response?.data?.error?.message ?? 'Ödeme başlatılamadı.';
      Alert.alert('Hata', msg);
      onComplete(false);
    }
  };

  if (loading) {
    return (
      <View style={styles.container}>
        <ActivityIndicator size="large" color="#4F46E5" />
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Ödeme Onayı</Text>

      <View style={styles.card}>
        <Text style={styles.merchantLabel}>İşyeri</Text>
        <Text style={styles.merchantName}>{qrInfo?.merchantTitle}</Text>

        <View style={styles.divider} />

        <Text style={styles.amountLabel}>Tutar</Text>
        <Text style={styles.amount}>
          {qrInfo?.amount.toFixed(2)} {qrInfo?.currency}
        </Text>

        <Text style={styles.countdown}>Geçerlilik: {countdown} sn</Text>
      </View>

      <TouchableOpacity
        style={[styles.payButton, paying && styles.payButtonDisabled]}
        onPress={handlePay}
        disabled={paying}
      >
        {paying ? (
          <ActivityIndicator color="#fff" />
        ) : (
          <Text style={styles.payButtonText}>
            {qrInfo?.amount.toFixed(2)} {qrInfo?.currency} Öde
          </Text>
        )}
      </TouchableOpacity>

      <TouchableOpacity style={styles.cancelButton} onPress={() => onComplete(false)}>
        <Text style={styles.cancelText}>İptal</Text>
      </TouchableOpacity>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    justifyContent: 'center',
    paddingHorizontal: 24,
    backgroundColor: '#F9FAFB',
  },
  title: {
    fontSize: 24,
    fontWeight: '700',
    textAlign: 'center',
    marginBottom: 24,
    color: '#111827',
  },
  card: {
    backgroundColor: '#fff',
    borderRadius: 16,
    padding: 24,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.08,
    shadowRadius: 8,
    elevation: 3,
    marginBottom: 24,
  },
  merchantLabel: { color: '#6B7280', fontSize: 13, marginBottom: 4 },
  merchantName: { fontSize: 20, fontWeight: '600', color: '#111827', marginBottom: 16 },
  divider: { height: 1, backgroundColor: '#E5E7EB', marginBottom: 16 },
  amountLabel: { color: '#6B7280', fontSize: 13, marginBottom: 4 },
  amount: { fontSize: 32, fontWeight: '700', color: '#111827', marginBottom: 12 },
  countdown: { color: '#9CA3AF', fontSize: 13, textAlign: 'right' },
  payButton: {
    backgroundColor: '#4F46E5',
    borderRadius: 12,
    padding: 18,
    alignItems: 'center',
    marginBottom: 12,
  },
  payButtonDisabled: { backgroundColor: '#9CA3AF' },
  payButtonText: { color: '#fff', fontSize: 18, fontWeight: '700' },
  cancelButton: { padding: 12, alignItems: 'center' },
  cancelText: { color: '#6B7280', fontSize: 16 },
});
