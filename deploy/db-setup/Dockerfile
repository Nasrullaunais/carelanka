FROM postgres:16-alpine
COPY docs/seed/ /seed/
COPY deploy/db-setup/app-role.sql /db-setup/
COPY --chmod=755 deploy/db-setup/db-setup.sh /usr/local/bin/db-setup
USER postgres
ENTRYPOINT ["db-setup"]
