# DWSIM OTS Deployment Guide

This guide covers deployment of the DWSIM Operator Training System in production environments.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Docker Compose Deployment](#docker-compose-deployment)
3. [Kubernetes Deployment](#kubernetes-deployment)
4. [Database Setup](#database-setup)
5. [Security Considerations](#security-considerations)
6. [Monitoring & Logging](#monitoring--logging)
7. [Backup & Recovery](#backup--recovery)
8. [Scaling](#scaling)
9. [Troubleshooting](#troubleshooting)

## Prerequisites

### Minimum System Requirements

#### Single-Host Development
- **CPU**: 4 cores
- **RAM**: 8 GB
- **Storage**: 50 GB SSD
- **OS**: Linux (Ubuntu 20.04+) or Windows Server 2019+

#### Production Multi-Host
- **Orchestrator**:
  - CPU: 2 cores
  - RAM: 4 GB
  - Storage: 20 GB

- **DWSIM Host** (per instance):
  - CPU: 4 cores
  - RAM: 8 GB (DWSIM is memory-intensive)
  - Storage: 100 GB (for flowsheets and snapshots)

- **TimescaleDB**:
  - CPU: 4 cores
  - RAM: 16 GB
  - Storage: 500 GB SSD (depends on retention policies)

### Software Requirements

- Docker 24.0+ and Docker Compose 2.20+
- .NET 8.0 Runtime
- DWSIM flowsheet files (.dwxmz format)
- Valid SSL certificates (for production)

## Docker Compose Deployment

### Production Configuration

1. **Clone the repository**:
   ```bash
   git clone https://github.com/KURIANGEORGE57/Operator-Training-System-Based-on-DWSIM.git
   cd Operator-Training-System-Based-on-DWSIM/ots-dwsim
   ```

2. **Create production environment file**:
   ```bash
   cp .env.example .env.production
   ```

3. **Edit `.env.production`**:
   ```bash
   # Database
   POSTGRES_PASSWORD=<strong-password>
   TIMESCALE_DB_CONNECTION=Host=timescaledb;Database=ots;Username=postgres;Password=<strong-password>

   # Orchestrator
   ORCHESTRATOR_LOG_LEVEL=Information
   DWSIM_HOST_URLS=http://dwsim-host-1:5000,http://dwsim-host-2:5000

   # UI
   REACT_APP_API_URL=https://your-domain.com/api

   # Security
   ENABLE_HTTPS=true
   CERTIFICATE_PATH=/certs/your-cert.pfx
   CERTIFICATE_PASSWORD=<cert-password>
   ```

4. **Create production docker-compose override**:

   Create `docker-compose.prod.yml`:
   ```yaml
   version: '3.8'

   services:
     orchestrator:
       environment:
         - ASPNETCORE_ENVIRONMENT=Production
         - ConnectionStrings__TimescaleDb=${TIMESCALE_DB_CONNECTION}
         - Logging__LogLevel__Default=Information
       volumes:
         - ./certs:/app/certs:ro
         - ./logs/orchestrator:/app/logs
       restart: always
       deploy:
         resources:
           limits:
             cpus: '2'
             memory: 4G

     dwsim-host:
       environment:
         - ASPNETCORE_ENVIRONMENT=Production
         - FlowsheetsPath=/flowsheets
         - SnapshotsPath=/snapshots
       volumes:
         - ./flowsheets:/flowsheets:ro
         - ./snapshots:/snapshots
         - ./logs/dwsim-host:/app/logs
       restart: always
       deploy:
         resources:
           limits:
             cpus: '4'
             memory: 8G
         replicas: 2

     timescaledb:
       environment:
         - POSTGRES_PASSWORD=${POSTGRES_PASSWORD}
         - PGDATA=/var/lib/postgresql/data/pgdata
       volumes:
         - timescale-data:/var/lib/postgresql/data
         - ./backups:/backups
       restart: always
       deploy:
         resources:
           limits:
             cpus: '4'
             memory: 16G

     hmi-operator:
       restart: always
       deploy:
         resources:
           limits:
             cpus: '1'
             memory: 512M

     hmi-instructor:
       restart: always
       deploy:
         resources:
           limits:
             cpus: '1'
             memory: 512M

   volumes:
     timescale-data:
       driver: local
   ```

5. **Start services**:
   ```bash
   docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d
   ```

6. **Verify deployment**:
   ```bash
   # Check all services are running
   docker-compose ps

   # Check logs
   docker-compose logs -f orchestrator

   # Test health endpoint
   curl http://localhost:5001/api/v1/health
   ```

### HTTPS/TLS Configuration

1. **Generate or obtain SSL certificate**:
   ```bash
   # Self-signed (development only)
   openssl req -x509 -newkey rsa:4096 -keyout key.pem -out cert.pem -days 365 -nodes
   openssl pkcs12 -export -out certificate.pfx -inkey key.pem -in cert.pem

   # Production: Use Let's Encrypt or your organization's CA
   ```

2. **Configure reverse proxy** (Nginx example):
   ```nginx
   server {
       listen 443 ssl http2;
       server_name your-domain.com;

       ssl_certificate /etc/ssl/certs/your-cert.crt;
       ssl_certificate_key /etc/ssl/private/your-key.key;

       # Orchestrator API
       location /api/ {
           proxy_pass http://localhost:5001/api/;
           proxy_http_version 1.1;
           proxy_set_header Upgrade $http_upgrade;
           proxy_set_header Connection "upgrade";
           proxy_set_header Host $host;
           proxy_cache_bypass $http_upgrade;
       }

       # Operator HMI
       location /operator/ {
           proxy_pass http://localhost:3000/;
           proxy_set_header Host $host;
       }

       # Instructor Station
       location /instructor/ {
           proxy_pass http://localhost:3001/;
           proxy_set_header Host $host;
       }
   }
   ```

## Kubernetes Deployment

### Prerequisites
- Kubernetes cluster (1.24+)
- kubectl configured
- Helm 3.0+
- Persistent Volume support

### Namespace Setup

```bash
kubectl create namespace dwsim-ots
kubectl config set-context --current --namespace=dwsim-ots
```

### TimescaleDB Deployment

```yaml
# timescaledb-deployment.yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: timescaledb-pvc
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 500Gi
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: timescaledb
spec:
  replicas: 1
  selector:
    matchLabels:
      app: timescaledb
  template:
    metadata:
      labels:
        app: timescaledb
    spec:
      containers:
      - name: timescaledb
        image: timescale/timescaledb:latest-pg15
        env:
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: db-secrets
              key: password
        - name: PGDATA
          value: /var/lib/postgresql/data/pgdata
        ports:
        - containerPort: 5432
        volumeMounts:
        - name: data
          mountPath: /var/lib/postgresql/data
        - name: init-sql
          mountPath: /docker-entrypoint-initdb.d
        resources:
          requests:
            memory: "8Gi"
            cpu: "2"
          limits:
            memory: "16Gi"
            cpu: "4"
      volumes:
      - name: data
        persistentVolumeClaim:
          claimName: timescaledb-pvc
      - name: init-sql
        configMap:
          name: db-init-sql
---
apiVersion: v1
kind: Service
metadata:
  name: timescaledb
spec:
  selector:
    app: timescaledb
  ports:
  - port: 5432
    targetPort: 5432
  type: ClusterIP
```

### Orchestrator Deployment

```yaml
# orchestrator-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: orchestrator
spec:
  replicas: 2
  selector:
    matchLabels:
      app: orchestrator
  template:
    metadata:
      labels:
        app: orchestrator
    spec:
      containers:
      - name: orchestrator
        image: your-registry/dwsim-ots-orchestrator:latest
        env:
        - name: ConnectionStrings__TimescaleDb
          valueFrom:
            secretKeyRef:
              name: db-secrets
              key: connection-string
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        ports:
        - containerPort: 5001
        livenessProbe:
          httpGet:
            path: /api/v1/health
            port: 5001
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /api/v1/health
            port: 5001
          initialDelaySeconds: 10
          periodSeconds: 5
        resources:
          requests:
            memory: "2Gi"
            cpu: "1"
          limits:
            memory: "4Gi"
            cpu: "2"
---
apiVersion: v1
kind: Service
metadata:
  name: orchestrator
spec:
  selector:
    app: orchestrator
  ports:
  - port: 5001
    targetPort: 5001
  type: LoadBalancer
```

### DWSIM Host StatefulSet

```yaml
# dwsim-host-statefulset.yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: dwsim-host
spec:
  serviceName: dwsim-host
  replicas: 3
  selector:
    matchLabels:
      app: dwsim-host
  template:
    metadata:
      labels:
        app: dwsim-host
    spec:
      containers:
      - name: dwsim-host
        image: your-registry/dwsim-ots-host:latest
        env:
        - name: FlowsheetsPath
          value: "/flowsheets"
        - name: SnapshotsPath
          value: "/snapshots"
        ports:
        - containerPort: 5000
        volumeMounts:
        - name: flowsheets
          mountPath: /flowsheets
          readOnly: true
        - name: snapshots
          mountPath: /snapshots
        resources:
          requests:
            memory: "4Gi"
            cpu: "2"
          limits:
            memory: "8Gi"
            cpu: "4"
      volumes:
      - name: flowsheets
        persistentVolumeClaim:
          claimName: flowsheets-pvc
  volumeClaimTemplates:
  - metadata:
      name: snapshots
    spec:
      accessModes: [ "ReadWriteOnce" ]
      resources:
        requests:
          storage: 100Gi
---
apiVersion: v1
kind: Service
metadata:
  name: dwsim-host
spec:
  selector:
    app: dwsim-host
  ports:
  - port: 5000
    targetPort: 5000
  clusterIP: None  # Headless service for StatefulSet
```

### Apply Kubernetes Manifests

```bash
# Create secrets
kubectl create secret generic db-secrets \
  --from-literal=password='your-strong-password' \
  --from-literal=connection-string='Host=timescaledb;Database=ots;Username=postgres;Password=your-strong-password'

# Create ConfigMap for init SQL
kubectl create configmap db-init-sql --from-file=init.sql=./src/infra/db/init.sql

# Apply deployments
kubectl apply -f timescaledb-deployment.yaml
kubectl apply -f orchestrator-deployment.yaml
kubectl apply -f dwsim-host-statefulset.yaml

# Check status
kubectl get pods
kubectl get services
```

## Database Setup

### Initial Schema Creation

The schema is automatically created by the TimescaleDB init script. For manual setup:

```bash
# Connect to database
docker exec -it timescaledb psql -U postgres

# Run init script
\i /docker-entrypoint-initdb.d/init.sql

# Verify tables
\dt
\d process_variables
\d session_events
```

### Database Tuning

Edit PostgreSQL configuration for production workloads:

```bash
# Edit postgresql.conf in TimescaleDB container
docker exec -it timescaledb bash
vi /var/lib/postgresql/data/postgresql.conf
```

Recommended settings for 16GB RAM:

```conf
shared_buffers = 4GB
effective_cache_size = 12GB
maintenance_work_mem = 1GB
work_mem = 64MB
max_connections = 200
max_worker_processes = 8
max_parallel_workers_per_gather = 4
max_parallel_workers = 8

# TimescaleDB specific
timescaledb.max_background_workers = 8
```

Restart database after changes:
```bash
docker-compose restart timescaledb
```

## Security Considerations

### 1. Database Security

- **Change default passwords**:
  ```bash
  docker exec -it timescaledb psql -U postgres
  ALTER USER postgres WITH PASSWORD 'new-strong-password';
  ```

- **Limit network access**:
  - Use firewall rules to restrict database access
  - Only allow Orchestrator IP addresses

- **Enable SSL for PostgreSQL**:
  ```conf
  ssl = on
  ssl_cert_file = '/var/lib/postgresql/server.crt'
  ssl_key_file = '/var/lib/postgresql/server.key'
  ```

### 2. API Security

- **Enable authentication** (future enhancement):
  - Implement JWT authentication
  - Add role-based access control (RBAC)

- **Rate limiting**:
  - Use reverse proxy (Nginx) for rate limiting
  - Configure per-IP limits

- **Input validation**:
  - Already implemented in API controllers
  - Validate all scenario JSON before execution

### 3. Network Security

- **Use HTTPS only** for all web traffic
- **Implement VPN** for remote access
- **Firewall rules**:
  ```bash
  # Allow only necessary ports
  ufw allow 443/tcp    # HTTPS
  ufw allow 22/tcp     # SSH (from specific IPs only)
  ufw enable
  ```

## Monitoring & Logging

### Application Logging

Logs are written to:
- Orchestrator: `/app/logs/orchestrator-*.log`
- DWSIM Host: `/app/logs/dwsim-host-*.log`

Mount these directories for persistence:
```yaml
volumes:
  - ./logs/orchestrator:/app/logs
```

### Health Monitoring

- **Health endpoints**:
  - Orchestrator: `GET /api/v1/health`
  - DWSIM Host: `GET /health`

- **Prometheus integration** (future):
  ```csharp
  // Add to Program.cs
  builder.Services.AddPrometheusExporter();
  app.MapMetrics();
  ```

### Log Aggregation

Use ELK Stack or similar:

```yaml
# docker-compose.logging.yml
services:
  elasticsearch:
    image: elasticsearch:8.10.0
    environment:
      - discovery.type=single-node
    volumes:
      - es-data:/usr/share/elasticsearch/data

  logstash:
    image: logstash:8.10.0
    volumes:
      - ./logstash.conf:/usr/share/logstash/pipeline/logstash.conf
      - ./logs:/logs:ro

  kibana:
    image: kibana:8.10.0
    ports:
      - "5601:5601"
    depends_on:
      - elasticsearch
```

## Backup & Recovery

### Database Backup

**Automated daily backups**:

```bash
#!/bin/bash
# backup-db.sh

BACKUP_DIR="/backups"
DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="$BACKUP_DIR/ots_backup_$DATE.sql"

docker exec timescaledb pg_dump -U postgres ots > $BACKUP_FILE
gzip $BACKUP_FILE

# Keep only last 30 days
find $BACKUP_DIR -name "ots_backup_*.sql.gz" -mtime +30 -delete

echo "Backup completed: $BACKUP_FILE.gz"
```

**Cron job**:
```bash
crontab -e
# Add line:
0 2 * * * /path/to/backup-db.sh >> /var/log/ots-backup.log 2>&1
```

**Restore from backup**:
```bash
gunzip ots_backup_20250120_020000.sql.gz
docker exec -i timescaledb psql -U postgres ots < ots_backup_20250120_020000.sql
```

### Snapshot Backup

Snapshot files are stored in `/snapshots` directory:

```bash
# Backup snapshots to S3 (example)
aws s3 sync /path/to/snapshots s3://your-bucket/ots-snapshots/ \
  --exclude "*.tmp" \
  --storage-class STANDARD_IA
```

## Scaling

### Horizontal Scaling

1. **Add DWSIM Host instances**:
   ```yaml
   # docker-compose.scale.yml
   services:
     dwsim-host-3:
       image: dwsim-ots-host
       ports:
         - "5004:5000"
       # ... same config as dwsim-host
   ```

2. **Update Orchestrator configuration**:
   ```json
   {
     "DwsimHosts": [
       { "Url": "http://dwsim-host-1:5000", "MaxSessions": 10 },
       { "Url": "http://dwsim-host-2:5000", "MaxSessions": 10 },
       { "Url": "http://dwsim-host-3:5000", "MaxSessions": 10 }
     ]
   }
   ```

3. **Scale using Docker Compose**:
   ```bash
   docker-compose up -d --scale dwsim-host=5
   ```

### Vertical Scaling

Increase resources for DWSIM hosts based on flowsheet complexity:

```yaml
deploy:
  resources:
    limits:
      cpus: '8'
      memory: 16G
```

### Database Scaling

For high-volume scenarios:
- Enable TimescaleDB read replicas
- Use connection pooling (PgBouncer)
- Partition by session_id for very large deployments

## Troubleshooting

### Common Production Issues

1. **High Memory Usage (DWSIM Host)**:
   - **Cause**: Complex flowsheets or too many concurrent sessions
   - **Solution**: Reduce MaxSessions per host, add more hosts, or increase RAM

2. **Database Connection Pool Exhausted**:
   - **Symptom**: "connection pool exhausted" errors
   - **Solution**: Increase max_connections in PostgreSQL or implement connection pooling

3. **Slow Query Performance**:
   - **Diagnosis**: Check slow query log
   - **Solution**: Add indexes, adjust retention policies, or archive old data

4. **Container Crashes**:
   - **Check logs**: `docker logs <container-name>`
   - **Check resources**: `docker stats`
   - **Increase limits** if OOM killed

### Performance Tuning

```bash
# Monitor TimescaleDB query performance
docker exec -it timescaledb psql -U postgres -d ots
SELECT * FROM pg_stat_statements ORDER BY total_time DESC LIMIT 10;

# Monitor compression ratios
SELECT * FROM timescaledb_information.compressed_chunk_stats;

# Monitor continuous aggregate refresh
SELECT * FROM timescaledb_information.continuous_aggregate_stats;
```

## Support

For deployment issues:
- Check logs: `docker-compose logs -f`
- Review troubleshooting section in [README.md](./README.md)
- Open an issue: https://github.com/KURIANGEORGE57/Operator-Training-System-Based-on-DWSIM/issues

---

**Document Version**: 1.0
**Last Updated**: 2025-01-20
