# Migrace UpdateHub z NASu na NUC

Cíl: UpdateHub běží na NUC (`10.0.1.44:8081`), DSM reverzní proxy na NASu (`10.0.1.22`) zůstává
a změní cíl z `localhost:8081` na `10.0.1.44:8081`. Veřejná adresa `https://updatehub.niderle.cz`,
CI token i port 443 na routeru se nemění.

UpdateHub je závislost ostatních aplikací (aktualizace klientů, nahrávání buildů z CI, stránky
ke stažení na niderle.cz, Dispatch). Výpadek je při migraci krátký, ale dělej ji, když neběží
žádný release workflow.

Data jsou ve složce `data` (SQLite `updatehub.db`, `artifacts/` s instalátory, `dp-keys/`).
Kontejner běží jako root (v Dockerfile není `USER`), takže se vlastnictví neřeší.

## 0. Příprava (bez výpadku)

Na NASu zjisti velikost dat (určí délku přenosu):

```bash
sudo du -sh /volume1/docker/updatehub/data
```

Na NUC musí být Docker, přihlášení do GHCR (`docker login ghcr.io`) a dost místa (`df -h /`).

Z Windows (samostatné PowerShell okno, ne SSH):

```powershell
ssh ladislav@10.0.1.44 "mkdir -p ~/updatehub"
scp E:\Projects\updatehub\deploy\nuc\docker-compose.yml ladislav@10.0.1.44:~/updatehub/docker-compose.yml
```

Na NUC:

```bash
cd ~/updatehub
ssh Elerdir@10.0.1.22 'cat /volume1/docker/updatehub/.env' > .env
chmod 600 .env
mkdir -p data logs
```

(Heslo na NAS zadej jednou a opatrně, opakované chyby NAS zablokuje IP adresu.)

## 1. Export na NASu (výpadek začíná)

```bash
cd /volume1/docker/updatehub && sudo /usr/local/bin/docker compose stop
sudo tar cf /volume1/docker/updatehub-data.tar data
sudo chown $(id -u):$(id -g) /volume1/docker/updatehub-data.tar
ls -lh /volume1/docker/updatehub-data.tar
tar tf /volume1/docker/updatehub-data.tar | head -n 8
```

Archiv není komprimovaný (instalátory už komprimované jsou). V ukázce musíš vidět `data/updatehub.db`
a `data/artifacts/`. Pokud je archiv prázdný, nepokračuj.

## 2. Import na NUC

```bash
cd ~/updatehub
ssh Elerdir@10.0.1.22 'cat /volume1/docker/updatehub-data.tar' > updatehub-data.tar
ls -lh updatehub-data.tar
sudo tar xf updatehub-data.tar -C .
docker compose up -d
curl -fsS http://10.0.1.44:8081/health
```

`sudo tar` zachová vlastníka a práva (`updatehub-data.tar` je v `~/updatehub`).

V DSM (Ovládací panel → Portál přihlášení → Upřesnit → Reverzní proxy) uprav pravidlo
`updatehub.niderle.cz`: cíl HTTP, `10.0.1.44`, port `8081`. WebSocket záhlaví nech.

Otestuj:
1. `https://updatehub.niderle.cz` a přihlášení do administrace, seznam aplikací a verzí.
2. Stažení instalátoru (odkaz ke stažení z `https://www.niderle.cz`).
3. Z klienta/aplikace kontrola aktualizace (`/api/apps/{slug}/update`).
4. Při příštím release ověř, že CI nahraje build (token zůstal stejný).

## 3. Návrat zpět

V DSM vrať cíl na `localhost:8081` a na NASu:

```bash
cd /volume1/docker/updatehub && sudo /usr/local/bin/docker compose start
```

Data na NASu zůstala, případně nahrané buildy od přepnutí by tam ale chyběly.

## 4. Zálohy (restic)

```bash
echo /home/ladislav/updatehub | sudo tee -a /etc/restic/paths
sudo systemctl start restic-backup.service
```

SQLite (`updatehub.db`) se zálohuje jako dump, instalátory v `artifacts/` inkrementálně.
Ověř, že NAS má pro zálohy dost místa (velikost `data` z kroku 0).

## 5. Úklid (po týdnu bez problémů)

Na NASu smaž projekt `updatehub` v Container Manageru, složku `/volume1/docker/updatehub`
a `/volume1/docker/updatehub-data.tar`. Na NUC smaž `~/updatehub/updatehub-data.tar`.

## Aktualizace

```bash
cd ~/updatehub && docker compose pull && docker compose up -d
```
