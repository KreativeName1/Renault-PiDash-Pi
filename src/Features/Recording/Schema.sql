CREATE TABLE IF NOT EXISTS sessions (
                                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                                        start_ts INTEGER NOT NULL,
                                        end_ts INTEGER
);

CREATE TABLE IF NOT EXISTS samples (
                                       session_id INTEGER NOT NULL,
                                       ts INTEGER NOT NULL,
                                       oil_kpa INTEGER NOT NULL,
                                       oiltemp_x10 INTEGER NOT NULL,
                                       vbat_mv INTEGER NOT NULL,
                                        gps_long_x10_6 INTEGER NOT NULL,
                                        gps_lat_x10_6 INTEGER NOT NULL,
                                        gps_speed_x10 INTEGER NOT NULL,
                                        roll_x10 INTEGER NOT NULL,
                                        pitch_x10 INTEGER NOT NULL,
                                        yaw_x10 INTEGER NOT NULL,
                                        accelX_mg INTEGER NOT NULL,
                                        accelY_mg INTEGER NOT NULL,
                                        accelZ_mg INTEGER NOT NULL,
                                        gyroX_x10 INTEGER NOT NULL,
                                        gyroY_x10 INTEGER NOT NULL,
                                        gyroZ_x10 INTEGER NOT NULL,
                                       light_lux INTEGER NOT NULL,
                                       FOREIGN KEY(session_id) REFERENCES sessions(id)
    );

CREATE INDEX IF NOT EXISTS idx_samples_session_ts ON samples(session_id, ts);
