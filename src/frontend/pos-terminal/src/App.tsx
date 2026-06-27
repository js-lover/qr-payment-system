// src/frontend/pos-terminal / src/App.tsx
//
// QR Ödeme POS Terminal React uygulaması.
//
// Akış:
//   login   → Kullanıcı adı / şifre girişi (MERCHANT veya TERMINAL rolü)
//   idle    → "Ödeme Başlat" butonu
//   amount  → Tutar girişi
//   qr      → QR kod gösterimi (90 sn countdown + durum polling)
//   waiting → Müşteri QR taradı, ödeme işleniyor (SignalR bekleme)
//   result  → APPROVED / DECLINED sonucu
//
// Ortam değişkenleri (.env.local):
//   VITE_API_BASE_URL   → Backend adresi (örn: http://localhost:8000)
//   VITE_TERMINAL_ID    → POS terminal ID (örn: "TID00001")
//   VITE_MERCHANT_ID    → İşyeri UUID
//   VITE_MERCHANT_TITLE → İşyeri adı

import React, { useState, useEffect, useRef } from 'react';
import axios from 'axios';
import * as signalR from '@microsoft/signalr';
import { QRCodeSVG } from 'qrcode.react';
import './App.css';

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8000';

type Stage = 'login' | 'idle' | 'amount' | 'qr' | 'waiting' | 'result';
type PaymentResult = 'approved' | 'declined';

interface QrData {
  token: string;
  qrContent: string;
  amount: number;
  merchantTitle: string;
  remainingSeconds: number;
}

export default function App() {
  const [stage, setStage] = useState<Stage>(() =>
    localStorage.getItem('terminalToken') ? 'idle' : 'login'
  );
  const [token, setToken] = useState<string>(() => localStorage.getItem('terminalToken') ?? '');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [loginError, setLoginError] = useState('');
  const [amount, setAmount] = useState('');
  const [qrData, setQrData] = useState<QrData | null>(null);
  const [result, setResult] = useState<PaymentResult | null>(null);
  const [countdown, setCountdown] = useState(90);
  const [loading, setLoading] = useState(false);
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  // Countdown timer (qr ve waiting stage'lerinde çalışır)
  useEffect(() => {
    if (stage !== 'qr' && stage !== 'waiting') return;
    if (countdown <= 0) {
      handleReset();
      return;
    }
    const t = setTimeout(() => setCountdown((c) => c - 1), 1000);
    return () => clearTimeout(t);
  }, [countdown, stage]);

  // QR durum polling — müşteri QR'ı tarayınca 'CLAIMED' olur → waiting stage
  useEffect(() => {
    if (stage !== 'qr' || !qrData) return;
    const poll = setInterval(async () => {
      try {
        const resp = await axios.get(`${API_BASE}/qr/${qrData.token}/status`, {
          headers: { Authorization: `Bearer ${token}` },
        });
        if (resp.data?.data?.status === 'CLAIMED') {
          setStage('waiting');
        }
      } catch {
        // polling hatası görmezden gel
      }
    }, 2000);
    return () => clearInterval(poll);
  }, [stage, qrData, token]);

  const handleLogin = async () => {
    setLoginError('');
    setLoading(true);
    try {
      const resp = await axios.post(`${API_BASE}/auth/token`, { username, password });
      const accessToken: string = resp.data?.data?.accessToken;
      if (!accessToken) throw new Error('Token alınamadı.');
      localStorage.setItem('terminalToken', accessToken);
      setToken(accessToken);
      setStage('idle');
    } catch {
      setLoginError('Kullanıcı adı veya şifre hatalı.');
    } finally {
      setLoading(false);
    }
  };

  const handleGenerateQr = async () => {
    const amountNum = parseFloat(amount.replace(',', '.'));
    if (isNaN(amountNum) || amountNum <= 0) return;

    setLoading(true);
    try {
      const resp = await axios.post(
        `${API_BASE}/qr/generate`,
        {
          terminalId: import.meta.env.VITE_TERMINAL_ID ?? '00000000-0000-0000-0000-000000000001',
          merchantId: import.meta.env.VITE_MERCHANT_ID ?? '00000000-0000-0000-0000-000000000002',
          merchantTitle: import.meta.env.VITE_MERCHANT_TITLE ?? 'İşyeri',
          amount: amountNum,
        },
        { headers: { Authorization: `Bearer ${token}` } }
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

      // SignalR bağlantısı QR oluşturulunca kurulur (müşteri tararken hazır olsun)
      setupSignalR(token);
    } catch (err: any) {
      if (err?.response?.status === 401) {
        handleLogout();
      } else {
        alert('QR oluşturulamadı. Lütfen tekrar deneyin.');
      }
    } finally {
      setLoading(false);
    }
  };

  const setupSignalR = (authToken: string) => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE}/hubs/payment`, {
        accessTokenFactory: () => authToken,
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
    connectionRef.current = null;
    setStage('amount');
    setQrData(null);
    setResult(null);
    setAmount('');
    setCountdown(90);
  };

  const handleLogout = () => {
    connectionRef.current?.stop();
    localStorage.removeItem('terminalToken');
    setToken('');
    setStage('login');
  };

  // ─── Ekranlar ──────────────────────────────────────────────────────────

  if (stage === 'login') {
    return (
      <div className="pos-container">
        <h1 className="pos-title">QR Ödeme Terminali</h1>
        <p className="pos-subtitle">Terminal girişi</p>
        <input
          className="amount-input"
          type="text"
          placeholder="Kullanıcı adı"
          value={username}
          onChange={(e) => setUsername(e.target.value)}
          autoFocus
        />
        <input
          className="amount-input"
          type="password"
          placeholder="Şifre"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && handleLogin()}
        />
        {loginError && <p className="error-text">{loginError}</p>}
        <button className="btn btn-primary" onClick={handleLogin} disabled={loading}>
          {loading ? 'Giriş yapılıyor...' : 'Giriş Yap'}
        </button>
      </div>
    );
  }

  if (stage === 'idle') {
    return (
      <div className="pos-container">
        <h1 className="pos-title">QR Ödeme Terminali</h1>
        <button className="btn btn-primary" onClick={() => setStage('amount')}>
          Ödeme Başlat
        </button>
        <button className="btn btn-secondary" style={{ marginTop: 12 }} onClick={handleLogout}>
          Çıkış Yap
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
