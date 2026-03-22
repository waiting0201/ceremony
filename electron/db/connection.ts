import * as sql from 'mssql';

let pool: sql.ConnectionPool | null = null;

function getConfig(): sql.config {
  const trusted = process.env['DB_TRUSTED'] === 'true';

  const config: sql.config = {
    server: process.env['DB_SERVER'] || 'localhost',
    database: process.env['DB_DATABASE'] || 'Ceremony',
    options: {
      encrypt: false,
      trustServerCertificate: true,
    },
    pool: {
      max: 10,
      min: 2,
      idleTimeoutMillis: 30000,
    },
  };

  if (trusted) {
    // Windows Authentication
    (config as any).authentication = {
      type: 'ntlm',
      options: {
        domain: '',
        userName: '',
        password: '',
      },
    };
    config.options!.trustedConnection = true;
  } else {
    // SQL Server Authentication
    config.user = process.env['DB_USER'] || 'sa';
    config.password = process.env['DB_PASSWORD'] || '';
  }

  return config;
}

export async function getPool(): Promise<sql.ConnectionPool> {
  if (pool && pool.connected) {
    return pool;
  }

  const config = getConfig();
  pool = new sql.ConnectionPool(config);
  await pool.connect();
  console.log('[DB] Connected to SQL Server:', config.server, '/', config.database);
  return pool;
}

export async function closePool(): Promise<void> {
  if (pool) {
    await pool.close();
    pool = null;
    console.log('[DB] Connection pool closed');
  }
}
