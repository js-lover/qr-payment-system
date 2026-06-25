// src/frontend/mobile-app / src/screens/QrScanScreen.tsx
//
// QR kod tarama ekranı.
// Expo kamerası ile QR kod okunur.
// QR içeriği "QRPAY:{uuid}" formatında parse edilir.
// UUID, ödeme onay ekranına iletilir.
//
// Kamera izni: Expo tarafından otomatik istenir.

import React, { useState } from 'react';
import { View, Text, StyleSheet, Alert } from 'react-native';
import { CameraView, useCameraPermissions } from 'expo-camera';

interface Props {
  onQrScanned: (token: string) => void;
}

export function QrScanScreen({ onQrScanned }: Props) {
  const [permission, requestPermission] = useCameraPermissions();
  const [scanned, setScanned] = useState(false);

  if (!permission) {
    return <View style={styles.container} />;
  }

  if (!permission.granted) {
    return (
      <View style={styles.container}>
        <Text style={styles.message}>Kamera erişimi gereklidir.</Text>
        <Text style={styles.link} onPress={requestPermission}>
          İzin Ver
        </Text>
      </View>
    );
  }

  const handleBarCodeScanned = ({ data }: { data: string }) => {
    if (scanned) return;

    // "QRPAY:{uuid}" formatını parse et
    if (!data.startsWith('QRPAY:')) {
      Alert.alert('Geçersiz QR', 'Bu bir QR ödeme kodu değil.');
      return;
    }

    setScanned(true);
    const token = data.replace('QRPAY:', '');
    onQrScanned(token);
  };

  return (
    <View style={styles.container}>
      <CameraView
        style={StyleSheet.absoluteFillObject}
        facing="back"
        onBarcodeScanned={scanned ? undefined : handleBarCodeScanned}
        barcodeScannerSettings={{ barcodeTypes: ['qr'] }}
      />
      <View style={styles.overlay}>
        <Text style={styles.overlayText}>QR Kodu Kameranıza Gösterin</Text>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#000',
  },
  message: {
    color: '#fff',
    textAlign: 'center',
    fontSize: 16,
    marginBottom: 16,
  },
  link: {
    color: '#818CF8',
    textAlign: 'center',
    fontSize: 16,
  },
  overlay: {
    position: 'absolute',
    bottom: 80,
    left: 0,
    right: 0,
    alignItems: 'center',
  },
  overlayText: {
    color: '#fff',
    fontSize: 16,
    fontWeight: '600',
    backgroundColor: 'rgba(0,0,0,0.5)',
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 8,
  },
});
