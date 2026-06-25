// src/frontend/pos-terminal / src/App.tsx
//
// QR Ödeme POS Terminal React uygulaması.
//
// Akış:
//   idle    → "Ödeme Başlat" butonu
//   amount  → Tutar girişi
//   qr      → QR kod gösterimi (90 sn countdown)
//   waiting → SignalR ile ödeme sonucu bekleme
//   result  → APPROVED / DECLINED sonucu
//
// Ortam değişkenleri (.env.local):
//   VITE_API_BASE_URL   → Kong gateway adresi (örn: http://localhost:8000)
//   VITE_TERMINAL_ID    → POS terminal ID (örn: "TID00001")
//   VITE_MERCHANT_ID    → İşyeri UUID
//   VITE_MERCHANT_TITLE → İşyeri adı

import React, { useState, useEffect, useRef } from 'react';
import axios from 'axios';
import * as signalR from '@microsoft/signalr';
import { QRCodeSVG } from 'qrcode.react';
import './App.css';

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8000';

type Stage = 'idle' | 'amount' | 'qr' | 'waiting' | 'result';
type PaymentResult = 'approved' | 'declined';

interface QrData {
  token: string;
  qrContent: string;
  amount: number;
  merchantTitle: string;
  remainingSeconds: number;
}

export default function App() {
  const [stage, setStage] = useState<Stage>('idle');
  const [amount, setAmount] = useState('');
  const [qrData, setQrData] = useState<QrData | null>(null);
  const [result, setResult] = useState<PaymentResult | null>(null);
  const [countdown, setCountdown] = useState(90);
  const [loading, setLoading] = useState(false);
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const terminalToken = localStorage.getItem('terminalToken') ?? '';

  // QR gösterildikten sonra countdown timer
  useEffect(() => {
    if (stage !== 'qr' && stage !== 'waiting') return;
    if (countdown <= 0) {
      handleReset();
      return;
    }
    const t = setTimeout(() => setCountdown((c) => c - 1), 1000);
    return () => clearTimeout(t);
  }, [countdown, stage]);

  const handleGenerateQr = async () => {
    const amountNum = parseFloat(amount.replace(',', '.'));
    if (isNaN(amountNum) || amountNum <= 0) return;

    setLoading(true);
    try {
      const resp = await axios.post(
        `${API_BASE}/qr/generate`,
        {
          terminalId: import.meta.env.VITE_TERMINAL_ID,
          merchantId: import.meta.env.VITE_MERCHANT_ID,
          merchantTitle: import.meta.env.VITE_MERCHANT_TITLE ?? 'İşyeri',
          amount: amountNum,
        },
        { headers: { Authorization: `Bearer ${terminalToken}` } }
      );

      const d = resp.data.data;
      setQrData({
        token: d.token,
        qrContent: d.qrContent,
        amount: d.amount,
        merchantTitle: d.merchantTitle,
        remainingSeconds: d.remainingSeconds,
      });
      setCountdown(d.remainingSeconds);
      setStage('qr');

      // SignalR bağlantısını QR ile birlikte kur (müşteri tarama öncesinde hazır ol)
      setupSignalR();
    } catch {
      alert('QR oluşturulamadı. Token geçerli mi?');
    } finally {
      setLoading(false);
    }
  };

  const setupSignalR = () => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE}/hubs/payment`, {
        accessTokenFactory: () => terminalToken,
      })
      .withAutomaticReconnect()
      .build();

    conn.on('PaymentResult', (r: { status: string }) => {
      const approved = r.status === 'COMPLETED';
      setResult(approved ? 'approved' : 'declined');
      setStage('result');
      conn.stop();
    });

    conn.start().catch(() => {
      console.error('SignalR bağlantı hatası');
    });

    connectionRef.current = conn;
  };

  const handleReset = () => {
    connectionRef.current?.stop();
    setStage('amount');
    setQrData(null);
    setResult(null);
    setAmount('');
    setCountdown(90);
  };

  // ─── Ekranlar ──────────────────────────────────────────────────────────

  if (stage === 'idle') {
    return (
      <div className="pos-container">
        <h1 className="pos-title">QR Ödeme Terminali</h1>
        <button className="btn btn-primary" onClick={() => setStage('amount')}>
          Ödeme Başlat
        </button>
      </div>
    );
  }

  if (stage === 'amount') {
    return (
      <div className="pos-container">
        <h2 className="pos-title">Tutar Giriniz</h2>
        <input
          className="amount-input"
          type="number"
          step="0.01"
          min="0.01"
          value={amount}
          onChange={(e) => setAmount(e.target.value)}
          placeholder="0,00"
          autoFocus
        />
        <button
          className="btn btn-primary"
          onClick={handleGenerateQr}
          disabled={loading}
        >
          {loading ? 'Yükleniyor...' : 'QR Oluştur'}
        </button>
        <button className="btn btn-secondary" onClick={() => setStage('idle')}>
          İptal
        </button>
      </div>
    );
  }

  if (stage === 'qr') {
    return (
      <div className="pos-container">
        <h2 className="pos-title">{qrData?.merchantTitle}</h2>
        <p className="pos-amount">{qrData?.amount.toFixed(2)} TL</p>
        <div className="qr-wrapper">
          <QRCodeSVG value={qrData?.qrContent ?? ''} size={260} />
        </div>
        <p className="countdown">{countdown} saniye</p>
        <button className="btn btn-secondary" onClick={handleReset}>
          İptal
        </button>
      </div>
    );
  }

  if (stage === 'waiting') {
    return (
      <div className="pos-container">
        <div className="spinner" />
        <p className="wait-text">Ödeme işleniyor...</p>
        <p className="countdown">{countdown} sn</p>
      </div>
    );
  }

  if (stage === 'result') {
    const approved = result === 'approved';
    return (
      <div className={`pos-container result-screen ${approved ? 'approved' : 'declined'}`}>
        <div className="result-icon">{approved ? '✅' : '❌'}</div>
        <h2 className="result-title">{approved ? 'ONAYLANDI' : 'REDDEDİLDİ'}</h2>
        <p className="pos-amount">{qrData?.amount.toFixed(2)} TL</p>
        <button className="btn btn-primary" onClick={handleReset}>
          Yeni İşlem
        </button>
      </div>
    );
  }

  return null;
}
