﻿import AboutViewFloating, {AboutViewAnchored, AccordionItemWrapper} from "components/common/AboutView";
import AccordionLicenseNotIncluded from "components/common/AccordionLicenseNotIncluded";
import { licenseSelectors } from "components/common/shell/licenseSlice";
import { useAppSelector } from "components/store";
import React from "react";
import {Icon} from "components/common/Icon";
import {useRavenLink} from "hooks/useRavenLink";

export function EditSqlEtlInfoHub() {
    const isProfessionalOrAbove = useAppSelector(licenseSelectors.isProfessionalOrAbove());
    const sqlEtlDocsLink = useRavenLink({ hash: "7J6SEO" });

    return (
        <AboutViewFloating defaultOpen={!isProfessionalOrAbove}>
            <AccordionItemWrapper
                targetId="about"
                icon="about"
                color="info"
                heading="About this view"
                description="Get additional info on this feature"
            >
                <p>
                    Text
                </p>
                <hr />
                <div className="small-label mb-2">useful links</div>
                <a href={sqlEtlDocsLink} target="_blank">
                    <Icon icon="newtab" /> Docs - SQL ETL
                </a>
            </AccordionItemWrapper>
            <AboutViewAnchored
                className="mt-2"
                defaultOpen={isProfessionalOrAbove ? null : "licensing"}
            >
                <AccordionLicenseNotIncluded
                    targetId="licensing"
                    featureName="SQL ETL"
                    featureIcon="sql-etl"
                    checkedLicenses={["Professional", "Enterprise"]}
                    isLimited={!isProfessionalOrAbove}
                />
            </AboutViewAnchored>
        </AboutViewFloating>
    );
}