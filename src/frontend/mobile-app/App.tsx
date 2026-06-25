// src/frontend/mobile-app / App.tsx
//
// React Native Expo uygulaması — ana giriş noktası.
//
// Ekranlar (state machine ile basit navigasyon):
//   null          → Yükleniyor (token kontrolü)
//   login         → Kullanıcı girişi (JWT token alır)
//   scanner       → QR kod tarama (kamera)
//   confirm:{tok} → Ödeme onayı (tutar + işyeri bilgisi + SignalR)
//
// Paketler:
//   expo-camera       → QR tarama
//   @microsoft/signalr → ödeme sonucu WebSocket
//   axios             → API çağrıları
//   @react-native-async-storage/async-storage → token depolama

import React, { useState, useEffect } from 'react';
import { View, ActivityIndicator, StatusBar } from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { LoginScreen } from './src/screens/LoginScreen';
import { QrScanScreen } from './src/screens/QrScanScreen';
import { PaymentConfirmScreen } from './src/screens/PaymentConfirmScreen';

type Screen = 'loading' | 'login' | 'scanner' | 'confirm';

export default function App() {
  const [screen, setScreen] = useState<Screen>('loading');
  const [scannedToken, setScannedToken] = useState<string | null>(null);

  useEffect(() => {
    AsyncStorage.getItem('accessToken').then((token) => {
      setScreen(token ? 'scanner' : 'login');
    });
  }, []);

  if (screen === 'loading') {
    return (
      <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center', backgroundColor: '#F9FAFB' }}>
        <ActivityIndicator size="large" color="#4F46E5" />
      </View>
    );
  }

  if (screen === 'login') {
    return (
      <>
        <StatusBar barStyle="dark-content" />
        <LoginScreen onLoginSuccess={() => setScreen('scanner')} />
      </>
    );
  }

  if (screen === 'confirm' && scannedToken) {
    return (
      <>
        <StatusBar barStyle="dark-content" />
        <PaymentConfirmScreen
          qrToken={scannedToken}
          onComplete={() => {
            setScannedToken(null);
            setScreen('scanner');
          }}
        />
      </>
    );
  }

  return (
    <>
      <StatusBar barStyle="light-content" />
      <QrScanScreen
        onQrScanned={(token) => {
          setScannedToken(token);
          setScreen('confirm');
        }}
      />
    </>
  );
}
