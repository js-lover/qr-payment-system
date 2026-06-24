#!/usr/bin/env bash
# scripts/generate-jwt-keys.sh
#
# Auth Service için RSA anahtar çifti üretir.
# Dev ortamında bir kez çalıştırılır; üretilen dosyalar .gitignore'dadır.
#
# Kullanım: ./scripts/generate-jwt-keys.sh

set -e

CERT_DIR="src/backend/services/AuthService/AuthService.Api/certs"
mkdir -p "$CERT_DIR"

echo "RSA 2048-bit özel anahtar üretiliyor..."
openssl genrsa -out "$CERT_DIR/jwt-private.pem" 2048

echo "Genel anahtar çıkarılıyor..."
openssl rsa -in "$CERT_DIR/jwt-private.pem" -pubout -out "$CERT_DIR/jwt-public.pem"

echo "✓ Anahtarlar oluşturuldu:"
echo "  Özel: $CERT_DIR/jwt-private.pem"
echo "  Genel: $CERT_DIR/jwt-public.pem"
echo ""
echo "NOT: Bu dosyalar .gitignore kapsamındadır ve commit'lenmez."
