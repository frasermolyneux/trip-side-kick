FROM postgres:18-bookworm AS postgres
RUN install -d -o 999 -g 1654 -m 0700 /var/lib/postgresql/data
COPY deployment/postgres-entrypoint.sh /entrypoint-source
RUN sed -i 's/\r$//' /entrypoint-source && chmod 0555 /entrypoint-source

FROM scratch
COPY --from=postgres / /
ENV PATH=/usr/lib/postgresql/18/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin \
    LANG=en_US.utf8 PGDATA=/var/lib/postgresql/data
COPY --from=postgres /entrypoint-source /usr/local/bin/tripsidekick-postgres
USER 999:1654
EXPOSE 5432
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=3 \
    CMD ["pg_isready", "--host=127.0.0.1", "--username=tripsidekick", "--dbname=tripsidekick"]
ENTRYPOINT ["/usr/local/bin/tripsidekick-postgres"]
